using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Drives a hand between an armament's resting place and the grip: the reach path, the elbow, the
	/// bone reads, and the physical placement of what is stowed or drawn. A plain class — not a
	/// MonoBehaviour — created and owned by the performer that decides WHAT should happen; this only
	/// ever decides HOW a hand gets there.
	/// The performer supplies the transition to advance and hears back through <see cref="StowReached"/>,
	/// <see cref="DrawReached"/> and <see cref="ArmSettled"/> rather than being called into directly, so
	/// this class never needs to know what wielding or equipping actually means.
	/// </summary>
	public class ArmSwapAnimator
	{
		/// <summary>How far over the shoulder, in arm lengths, a grip must sit to be reached over it.</summary>
		private const float OVER_SHOULDER_MARGIN = 0.1f;

		/// <summary>Share of the arm a slide may use. Below 1 it stops short of a locked-out arm.</summary>
		private const float SLIDE_REACH = 0.8f;

		/// <summary>Where a reach behind the shoulder passes, above and in front of it, in arm lengths.</summary>
		private const float OVER_SHOULDER_RISE = 0.35f;
		private const float OVER_SHOULDER_LEAD = 0.4f;

		/// <summary>Invoked when a ToStow leg lands: the item is at rest and may let go of the hand.</summary>
		public event Action<ArmState, RuntimeEquipedData> StowReached;

		/// <summary>Invoked when a ToDraw leg lands with something to draw: the grip is seated in hand.</summary>
		public event Action<ArmState, RuntimeEquipedData> DrawReached;

		/// <summary>
		/// Invoked once an arm's hand has nothing left to reach for this swap — whether it just finished
		/// drawing, or was never going to draw anything at all. The moment to rest what is not wielded.
		/// </summary>
		public event Action<ArmState> ArmSettled;

		/// <summary>Log every leg's path and elbow as it happens. Off by default — zero cost when off,
		/// since every log site is gated behind this check before it builds anything.</summary>
		public bool DebugLogging;

		/// <summary>Draw the yellow hand-to-anchor line while a path is built. Mirrors the owner's toggle.</summary>
		public bool DrawGizmos;

		/// <summary>The object every IK influencer is filed under, so add and remove always agree on who
		/// is asking — regardless of which of this class's methods happens to make the call.</summary>
		private readonly object owner;

		private readonly TransformLookup lookup;
		private readonly IAgentBody agentBody;
		private readonly Transform agentTransform;
		private readonly IIKComponent ik;
		private readonly AgentSheatheComponent sheathe;
		private readonly int ikPriority;
		private readonly float handSpeed;
		private readonly float minLegDuration;
		private readonly float arcBulge;
		private readonly EasingMethod gripEasing;

		/// <summary>Keeps each elbow smooth, out of the body and inside its cone. Owns its own cone cache.</summary>
		private readonly ElbowHintSolver elbows = new ElbowHintSolver();

		/// <summary>The torso's own capsules, refilled each time a path is built rather than reallocated.</summary>
		private readonly List<BodyCapsule> bodyCapsules = new List<BodyCapsule>();
		private BodyCapsule[] bodyCapsuleBuffer;

		/// <summary>Last frame's animated hand poses, in torso space. See <see cref="SampleAnimatedHands"/>.</summary>
		private (Vector3 pos, Quaternion rot) leftAnimated;
		private (Vector3 pos, Quaternion rot) rightAnimated;

		/// <summary>The animated arms' own shoulder-to-hand line and elbow direction off it, in agent space.</summary>
		private (Vector3 axis, Vector3 roll) leftSwivel;
		private (Vector3 axis, Vector3 roll) rightSwivel;
		private bool sampledAnimated;

		public ArmSwapAnimator(object owner, TransformLookup lookup, IAgentBody agentBody,
			Transform agentTransform, IIKComponent ik, AgentSheatheComponent sheathe, int ikPriority,
			float handSpeed, float minLegDuration, float arcBulge, EasingMethod gripEasing)
		{
			this.owner = owner;
			this.lookup = lookup;
			this.agentBody = agentBody;
			this.agentTransform = agentTransform;
			this.ik = ik;
			this.sheathe = sheathe;
			this.ikPriority = ikPriority;
			this.handSpeed = handSpeed;
			this.minLegDuration = minLegDuration;
			this.arcBulge = arcBulge;
			this.gripEasing = gripEasing;
		}

		private Transform LeftHand => lookup.Lookup(HumanBoneIdentifiers.LEFT_HAND);
		private Transform RightHand => lookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND);
		private Transform LeftSheathe => lookup.Lookup(TransformLookupIdentifiers.LEFT_SHEATHE);
		private Transform RightSheathe => lookup.Lookup(TransformLookupIdentifiers.RIGHT_SHEATHE);

		#region Starting and advancing a leg

		/// <summary>
		/// Starts one arm's leg sequence: captures where the hand actually is, measures how its next
		/// armament slides clear of its own sheathe, reserves the resting place being vacated, and begins
		/// the first leg. The caller owns the returned transition from here — <see cref="Advance"/> moves
		/// it forward, and it is spent once its <see cref="ArmTransition.Leg"/> reaches <c>Done</c>.
		/// </summary>
		public ArmTransition Begin(ArmState arm, RuntimeEquipedData stowing, RuntimeEquipedData drawing)
		{
			ArmTransition transition = new ArmTransition { Arm = arm, Stowing = stowing, Drawing = drawing };

			if (DebugLogging)
			{
				SpaxDebug.Log("ARMSWAP", $"{(arm.IsLeft ? "LEFT" : "RIGHT")} begins" +
					$" | stowing {Name(stowing)} | drawing {Name(drawing)}" +
					$" | holds {arm.Armaments.Count} of {arm.Slots.Count}");
			}

			// Where the arm was before we took it — recovery brings it back here.
			CaptureHome(transition);

			// How Drawing comes clear of ITS sheathe, while it is still sitting there to measure.
			(transition.WithdrawAxis, transition.WithdrawDepth) = InsertMotion(arm, drawing);

			// Reserve the resting place up front, so the hand has somewhere definite to reach for.
			if (sheathe != null && stowing != null)
			{
				sheathe.TryAssign(stowing, arm.Side, ArmUtils.StowOrder(arm, stowing));
			}

			BeginLeg(transition, stowing != null ? TransitionLeg.ToStow : TransitionLeg.ToDraw);
			return transition;
		}

		/// <summary>
		/// Advances one leg by <paramref name="delta"/>. Each leg's length is the distance the hand
		/// actually covers, so a swap between two armaments sharing a resting place costs almost nothing.
		/// Returns true once the whole transition has nothing left to do.
		/// </summary>
		public bool Advance(ArmTransition transition, float delta, bool paused)
		{
			if (!paused)
			{
				transition.Time += delta;
			}

			float progress = transition.Duration <= 0f ? 1f : Mathf.Clamp01(transition.Time / transition.Duration);
			float eased = progress.InOutCubic();

			ArmPath path = BuildPath(transition);
			(Vector3 pos, Quaternion rot, float arc) pose = path.Evaluate(eased);
			float authority = Authority(transition, pose.pos);
			float weight = transition.WeightAt(progress, authority);
			transition.Weight = weight;
			transition.Commitment = transition.CommitmentAt(eased);

			// The path decides the hand, and the elbow is only a bend preference again. It used to be the
			// other way about — the hand read off the elbow, and the elbow was fought over by five rules
			// that knew nothing of the grip, so the hand inherited every one of their arguments.
			Quaternion hand = pose.rot;
			ApplyElbowHint(transition, pose.pos, weight, authority, delta);

			ik.AddInfluencer(owner, transition.Arm.IKChain, ikPriority, pose.pos, weight, hand, weight);

			if (DebugLogging)
			{
				Transform handBone = transition.Arm.IsLeft ? LeftHand : RightHand;
				Quaternion twist = Quaternion.Inverse(agentTransform.rotation) * BodyFrame().rotation;

				// Where the armament actually POINTS, in the body's own frame. An angle says how far the
				// hand turned; this says where it ended up, which is the only thing that looks wrong.
				Quaternion held = transition.CarriedIn ? path.EndRotation : path.StartRotation;
				Vector3 axis = transition.CarriedIn ? path.EndAxis : path.StartAxis;
				Vector3 points = Quaternion.Inverse(BodyFrame().rotation) *
					(hand * (Quaternion.Inverse(held) * axis));

				// Same for the hand itself, so a wrist rolling under a steady blade is still visible.
				Vector3 palmUp = Quaternion.Inverse(BodyFrame().rotation) * (hand * Vector3.up);

				SpaxDebug.Log("ARMHAND",
					$"{(transition.Arm.IsLeft ? "LEFT" : "RIGHT")} {transition.Leg} t {progress:0.##} | w {weight:0.###}" +
					// askRot is what the IK failed to deliver of the rotation we ASKED for — a snap on grab
					// lives here. toEnd is how far our ask still is from the grip, and must reach 0.
					// roll is the leftover as WOUND. It is settled in direction but its SIZE tracks a live
					// end pose, so a jump here is the target moving, not the swing.
					$" | roll {path.Roll():0.#}" +
					$" askRot {Quaternion.Angle(hand, handBone.rotation):0.#}" +
					$" toEnd {Quaternion.Angle(hand, path.EndRotation):0.#}" +
					$" carry {Quaternion.Angle(path.StartRotation, hand):0.#}" +
					$" offPos {Vector3.Distance(pose.pos, handBone.position):0.###}" +
					// POINTS is where the armament aims, palmUp where the back of the hand faces, both in
					// body space. +x is the arm's own side, +y up, +z forward.
					$" | POINTS {points.ToString("F2")} palmUp {palmUp.ToString("F2")}" +
					// The palm frame the grip is aimed with, in the hand's own space. It is built from the
					// LIVE finger bones, so if it moves the approach and the attach are aiming differently.
					$" | palm {(Quaternion.Inverse(handBone.rotation) * GetHandSlotOrientation(transition.Arm.IsLeft, false).rot).eulerAngles}" +
					// Degrees the torso has turned against the root since home was pinned. The old framing
					// carried every one of these as error; the torso framing carries none.
					$" | drift {Quaternion.Angle(transition.HomeTwist, twist):0.#}");
			}

			if (progress >= 1f)
			{
				// What was SENT, not what the path asked for. Across the swing the path only ever reports
				// StartRotation, so freezing that hands the next leg a pose the hand was never in.
				FinishLeg(transition, pose.pos, hand);
			}

			return transition.Leg == TransitionLeg.Done;
		}

		private void FinishLeg(ArmTransition transition, Vector3 position, Quaternion rotation)
		{
			// Whatever comes next starts from exactly where this leg landed.
			Freeze(transition, position, rotation);

			switch (transition.Leg)
			{
				case TransitionLeg.ToStow:
					// The hand has arrived at the resting place: let go.
					StowReached?.Invoke(transition.Arm, transition.Stowing);
					BeginLeg(transition, TransitionLeg.ToDraw);
					break;

				case TransitionLeg.ToDraw:
					if (transition.Drawing != null)
					{
						DrawReached?.Invoke(transition.Arm, transition.Drawing);
					}
					ArmSettled?.Invoke(transition.Arm);

					BeginLeg(transition, TransitionLeg.Recover);
					break;

				case TransitionLeg.Recover:
					ik.RemoveInfluencer(owner, transition.Arm.IKChain);
					ik.RemoveHintInfluencer(owner, transition.Arm.IKChain);
					transition.Leg = TransitionLeg.Done;
					break;
			}
		}

		private void BeginLeg(ArmTransition transition, TransitionLeg leg)
		{
			transition.Leg = leg;
			transition.Time = 0f;

			switch (leg)
			{
				case TransitionLeg.ToStow:
					// Reach out from wherever the animation has the hand, and slide the armament home.
					FreezeHand(transition);
					transition.From = null;
					transition.To = transition.Stowing;
					transition.Blend = LegBlend.In;
					SetSlide(transition, null, transition.Stowing);
					transition.Clearance = HandRadius(transition.Arm.IsLeft);
					break;

				case TransitionLeg.ToDraw:
					if (transition.Drawing == null)
					{
						// Nothing to pick up — the stow already happened, so head home.
						ArmSettled?.Invoke(transition.Arm);
						BeginLeg(transition, TransitionLeg.Recover);
						return;
					}

					if (transition.Stowing != null)
					{
						// Carry on from the resting place we just left the old armament at.
						transition.From = transition.Stowing;
						transition.To = transition.Drawing;
						transition.Blend = LegBlend.Hold;
					}
					else
					{
						FreezeHand(transition);
						transition.From = null;
						transition.To = transition.Drawing;
						transition.Blend = LegBlend.In;
					}

					// The hand is empty the whole way over — it has nothing to draw out or push in. The
					// withdraw point exists only because a hand DRAGS an armament clear along its own axis;
					// with the armament still resting there, an empty hand goes straight to the grip.
					SetSlide(transition, null, null);
					transition.Clearance = HandRadius(transition.Arm.IsLeft);
					break;

				case TransitionLeg.Recover:
					// Draw clear of the sheathe, then swing home to where the animation left the hand.
					transition.From = null;
					transition.To = null;
					transition.Blend = LegBlend.Out;

					// Only something actually in hand has to come out first; an empty hand just leaves.
					SetSlide(transition, transition.Drawing, null);
					transition.Clearance = HandRadius(transition.Arm.IsLeft);
					break;
			}

			SetupLeg(transition);
		}

		/// <summary>
		/// The straight slide in and out of a sheathe at each end of the leg, held in agent space so it
		/// turns with the body.
		/// </summary>
		private void SetSlide(ArmTransition transition, RuntimeEquipedData leaving, RuntimeEquipedData arriving)
		{
			// Recover's leaving item is always Drawing, and by then it has already left its sheathe and
			// been reoriented to the grip — measuring fresh here would read that instead of the withdraw.
			(Vector3 axis, float depth) start = leaving != null && leaving == transition.Drawing
				? (transition.WithdrawAxis, transition.WithdrawDepth)
				: InsertMotion(transition.Arm, leaving);
			(Vector3 axis, float depth) end = InsertMotion(transition.Arm, arriving);

			// An empty hand carries nothing out, but it still has to come at the grip from somewhere, and
			// behind the shoulder there is only one way in. An armament's own slide already leaves that way.
			transition.StartGated = start.depth <= 0f;
			transition.EndGated = end.depth <= 0f;
			if (transition.StartGated)
			{
				start = OverShoulder(transition.Arm, LegStart(transition).pos);
				transition.StartGated = start.depth > 0f;
			}
			if (transition.EndGated)
			{
				end = OverShoulder(transition.Arm, LegEnd(transition).pos);
				transition.EndGated = end.depth > 0f;
			}

			transition.StartAxis = start.axis;
			transition.StartDepth = start.depth;
			transition.EndAxis = end.axis;
			transition.EndDepth = end.depth;

			// Which end the armament's own sheathe is at, and whether the arm has to get over the shoulder
			// to reach it — the only place the pose has a wrong way round to turn.
			transition.Carrying = leaving != null || arriving != null;
			transition.CarriedIn = arriving != null;
			transition.TurnsOver = transition.Carrying && OverShoulderGrip(transition.Arm,
				(transition.CarriedIn ? LegEnd(transition) : LegStart(transition)).pos);
		}

		/// <summary>
		/// Measures the leg once it is described, fixing how its time divides between sliding and swinging.
		/// </summary>
		private void SetupLeg(ArmTransition transition)
		{
			// Settled first, because it decides the shape of the arc the length is then measured along.
			// Either end over the shoulder makes the whole leg one, so a stow and its recover match.
			transition.ReachesOver = OverShoulderGrip(transition.Arm, LegStart(transition).pos) ||
				OverShoulderGrip(transition.Arm, LegEnd(transition).pos);

			ArmPath path = BuildPath(transition);
			path.SettleArc(transition.ReachesOver, out transition.ArcFront, out transition.ArcSide,
				out transition.StartFar, out transition.EndFar);
			path.SwingFront = transition.ArcFront;
			path.FlankSide = transition.ArcSide;
			path.StartFar = transition.StartFar;
			path.EndFar = transition.EndFar;

			// Settled off the arc, so it has the shape it will actually be measured along.
			transition.Turning = path.SettleWinding(out float windDot, out float windSwing, out float windShort);
			path.Turning = transition.Turning;

			float length = path.Length;

			transition.WithdrawFraction = length <= 0f ? 0f : transition.StartDepth / length;
			transition.InsertFraction = length <= 0f ? 0f : transition.EndDepth / length;
			transition.Duration = DurationFor(length);

			if (DebugLogging)
			{
				SpaxDebug.Log("ARMPATH",
					$"{(transition.Arm.IsLeft ? "LEFT" : "RIGHT")} {transition.Leg}" +
					// Which swap this leg belongs to, and which end of it is the armament's own place.
					$" | stowing {Name(transition.Stowing)} drawing {Name(transition.Drawing)}" +
					$" | from {Name(transition.From)} to {Name(transition.To)}" +
					$" | {BuildPath(transition).Describe()}" +
					$" | gated {(transition.StartGated ? 1 : 0)}/{(transition.EndGated ? 1 : 0)}" +
					// The height the over/under decision is taken against, and the joint it used to.
					$" | top {Height(ShoulderTop(transition.Arm)):0.###} joint {Height(Shoulder(transition.Arm)):0.###}" +
					// How far the path alone turns the hand, and the roll owed on top as WOUND: below zero
					// it is going the long way round, because the short way fights the swing.
					$" | swung {Quaternion.Angle(path.StartRotation, path.Landed()):0.#}" +
					$" roll {path.Roll():0.#}" +
					// The winding decision's own terms. opposed is the swing projected onto the roll's
					// axis — the degrees of the path that actually fight the short way round.
					$" [dot {windDot:0.##} swing {windSwing:0.#} short {windShort:0.#}" +
					$" opposed {windSwing * -windDot:0.#}]" +
					$" carrying {(transition.Carrying ? (transition.CarriedIn ? "in" : "out") : "-")}" +
					// The withdraw as measured at the sheathe, for comparing against this leg's actual depth.
					$" captured {transition.WithdrawDepth:0.###}" +
					$" over {(transition.TurnsOver ? 1 : 0)}" +
					$" | slotFrom {SlotInfo(transition.Arm, transition.From)} slotTo {SlotInfo(transition.Arm, transition.To)}");
			}
		}

		#endregion Starting and advancing a leg

		#region Path

		/// <summary>
		/// The hand's route for this leg, rebuilt from live endpoints so it follows a body that is moving.
		/// Only the time splits are held fixed, from when the leg began.
		/// </summary>
		public ArmPath BuildPath(ArmTransition transition)
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			(Vector3 pos, Quaternion rot) start = LegStart(transition);
			(Vector3 pos, Quaternion rot) end = LegEnd(transition);

			return new ArmPath
			{
				Origin = body.origin,
				Rotation = body.rotation,
				StartPosition = start.pos,
				StartRotation = start.rot,
				EndPosition = end.pos,
				EndRotation = end.rot,
				StartAxis = agentTransform.rotation * transition.StartAxis,
				StartDepth = transition.StartDepth,
				EndAxis = agentTransform.rotation * transition.EndAxis,
				EndDepth = transition.EndDepth,
				WithdrawFraction = transition.WithdrawFraction,
				InsertFraction = transition.InsertFraction,
				ClearanceOffset = transition.Clearance,
				Body = BodyCapsules(),
				Turning = transition.Turning,
				Carrying = transition.Carrying,
				SwingFront = transition.ArcFront,
				FlankSide = transition.ArcSide,
				OverShoulder = transition.ReachesOver,
				ShoulderLocal = Shoulder(transition.Arm) == null ? Vector3.zero
					: Quaternion.Inverse(BodyFrame().rotation) * (Shoulder(transition.Arm).position - BodyFrame().origin),
				Bulge = arcBulge,
				GripEasing = gripEasing,
				StartFar = transition.StartFar,
				EndFar = transition.EndFar,
				SideSign = transition.Arm.IsLeft ? -1f : 1f
			};
		}

		/// <summary>
		/// The torso's own collision capsules in the body's frame — the authored shape, not a guess at it.
		/// Refilled into one buffer so rebuilding the path every frame allocates nothing.
		/// </summary>
		public BodyCapsule[] BodyCapsules()
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Quaternion inverse = Quaternion.Inverse(body.rotation);

			bodyCapsules.Clear();
			for (int i = 0; i < ArmUtils.TORSO_BONES.Length; i++)
			{
				if (agentBody == null || !agentBody.TryGetBoneColliders(ArmUtils.TORSO_BONES[i], out IReadOnlyList<Collider> colliders))
				{
					continue;
				}

				for (int c = 0; c < colliders.Count; c++)
				{
					if (colliders[c] is CapsuleCollider capsule && ArmUtils.Capsule(capsule, inverse, body.origin,
						out BodyCapsule shape))
					{
						bodyCapsules.Add(shape);
					}
				}
			}

			if (bodyCapsuleBuffer == null || bodyCapsuleBuffer.Length != bodyCapsules.Count)
			{
				bodyCapsuleBuffer = new BodyCapsule[bodyCapsules.Count];
			}
			bodyCapsules.CopyTo(bodyCapsuleBuffer);
			return bodyCapsuleBuffer;
		}

		/// <summary>
		/// The torso as a solid to test against: its capsules and the frame they are expressed in.
		/// Shares <see cref="BodyCapsules"/>' buffer, so consume it before calling either again.
		/// </summary>
		public ElbowHintSolver.Torso Torso()
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return new ElbowHintSolver.Torso
			{
				Capsules = BodyCapsules(),
				Origin = body.origin,
				Rotation = body.rotation
			};
		}

		/// <summary>
		/// The pelvis' frame — the thing the arm is reaching around, and the one part of the torso the
		/// arm cannot move: full-body IK lets a reach drag the shoulders with it, so a frame taken from
		/// them would be partly an output of the reach it is meant to decide.
		/// Built from bone positions alone, all three rigid to the pelvis, so no rig's axis convention
		/// or animated twist can enter into it.
		/// </summary>
		public (Vector3 origin, Quaternion rotation) BodyFrame()
		{
			Transform hips = lookup.Lookup(HumanBoneIdentifiers.HIPS);
			Transform spine = lookup.Lookup(HumanBoneIdentifiers.SPINE);
			Transform leftLeg = lookup.Lookup(HumanBoneIdentifiers.LEFT_UPPER_LEG);
			Transform rightLeg = lookup.Lookup(HumanBoneIdentifiers.RIGHT_UPPER_LEG);

			if (hips == null || spine == null || leftLeg == null || rightLeg == null)
			{
				return (agentTransform.position, agentTransform.rotation);
			}

			// The leg bones' positions are the hip joints, so this spans the pelvis rather than the legs.
			// Spine hangs off the hips, so where it sits turns with the pelvis however the back bends.
			Vector3 across = rightLeg.position - leftLeg.position;
			Vector3 up = spine.position - hips.position;
			Vector3 forward = Vector3.Cross(across, up);

			if (forward.sqrMagnitude < 0.0001f || up.sqrMagnitude < 0.0001f)
			{
				return (agentTransform.position, agentTransform.rotation);
			}

			return (hips.position, Quaternion.LookRotation(forward, up));
		}

		/// <summary>Where this leg begins: an armament's resting place, or the pose the hand was caught in.</summary>
		private (Vector3 pos, Quaternion rot) LegStart(ArmTransition transition)
		{
			if (transition.From != null)
			{
				return GetSheathingOrientation(transition.Arm, transition.From);
			}

			// Taking over FROM the animation: read it LIVE, the way recovery reads where it hands back to.
			// Arming moves the idle out from under us, and the snapshot is caught in Update off last
			// frame's bones — so the hand reaches back down to a pose the body has already left.
			if (transition.Blend == LegBlend.In)
			{
				(Vector3 pos, Quaternion rot) caught = AnimatedHome(transition);
				return BodyPose(caught.pos, caught.rot);
			}

			return BodyPose(transition.FrozenPosition, transition.FrozenRotation);
		}

		/// <summary>Where this leg ends: an armament's resting place, or the pose the animation is holding.</summary>
		private (Vector3 pos, Quaternion rot) LegEnd(ArmTransition transition)
		{
			if (transition.To != null)
			{
				return GetSheathingOrientation(transition.Arm, transition.To);
			}

			// Live, not the pose captured when the arm was claimed — drawing changes the idle underneath us.
			(Vector3 pos, Quaternion rot) home = AnimatedHome(transition);
			return BodyPose(home.pos, home.rot);
		}

		/// <summary>
		/// Resolves a pose held in the torso's frame, so it tracks the body's twist and not just the
		/// root's heading, without feeding back off the hand it drives.
		/// </summary>
		private (Vector3 pos, Quaternion rot) BodyPose(Vector3 position, Quaternion rotation)
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return (body.origin + body.rotation * position, body.rotation * rotation);
		}

		/// <summary>
		/// How far this leg has taken the hand from where the animation wants it, in forearms. A shoulder's
		/// whole twist range spans about a forearm of hand travel, so that is the scale.
		/// </summary>
		private float Authority(ArmTransition transition, Vector3 hand)
		{
			Transform elbow = Elbow(transition.Arm);
			Transform handBone = transition.Arm.IsLeft ? LeftHand : RightHand;
			if (elbow == null || handBone == null)
			{
				return 1f;
			}

			(Vector3 pos, Quaternion rot) home = AnimatedHome(transition);
			float forearm = Vector3.Distance(elbow.position, handBone.position);
			float displaced = Vector3.Distance(hand, BodyPose(home.pos, home.rot).pos);
			return forearm < 0.0001f ? 1f : Mathf.Clamp01(displaced / forearm);
		}

		#endregion Path

		#region Elbow

		/// <summary>
		/// Puts the elbow where the hand's own orientation carries it — opposite the way the fingers point,
		/// on the ring its bones can actually reach — then holds it inside the shoulder's joint limit.
		/// </summary>
		private void ApplyElbowHint(ArmTransition transition, Vector3 hand,
			float weight, float authority, float delta)
		{
			Transform shoulder = Shoulder(transition.Arm);
			Transform elbow = Elbow(transition.Arm);
			Transform handBone = transition.Arm.IsLeft ? LeftHand : RightHand;
			if (shoulder == null || elbow == null)
			{
				return;
			}

			// The ANIMATION's elbow, turned by however far this arm has turned away from the pose it was
			// read in. ONE turn from the pinned original onto the line the arm has now — composing a step
			// per frame instead is parallel transport, and it accumulates the swept angle as a false twist.
			Quaternion agent = agentTransform.rotation;
			Vector3 armLine = Quaternion.Inverse(agent) * (hand - shoulder.position).normalized;
			(Vector3 axis, Vector3 roll) animated = transition.Arm.IsLeft ? leftSwivel : rightSwivel;

			if (!transition.HasSwivel && sampledAnimated && animated.axis != Vector3.zero)
			{
				transition.SwivelAxis = animated.axis;
				transition.SwivelSeed = animated.roll;
				transition.HasSwivel = true;
			}

			if (transition.HasSwivel)
			{
				transition.SwivelRoll = Quaternion.FromToRotation(transition.SwivelAxis, armLine) *
					transition.SwivelSeed;
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			ElbowHintSolver.Torso torso = new ElbowHintSolver.Torso
			{
				Capsules = BodyCapsules(),
				Origin = body.origin,
				Rotation = body.rotation
			};

			// What the elbow WANTS is all this class decides; the solver makes it legal.
			Vector3 wanted = transition.HasSwivel
				? agent * transition.SwivelRoll
				: -(body.rotation * Vector3.up);

			elbows.DebugLogging = DebugLogging;
			if (!elbows.TrySolve(transition.Elbow, shoulder, elbow, handBone, hand, wanted, torso,
				agent, transition.Clearance, delta, out Vector3 hint,
				out ElbowHintSolver.Report report))
			{
				return;
			}

			ik.AddHintInfluencer(owner, transition.Arm.IKChain, ikPriority, hint, weight);

			if (DebugLogging)
			{
				Quaternion inverse = Quaternion.Inverse(body.rotation);
				float progress = transition.Duration <= 0f ? 1f : transition.Time / transition.Duration;
				Vector3 outward = body.rotation * (transition.Arm.IsLeft ? Vector3.left : Vector3.right);
				Vector3 neutral = Vector3.ProjectOnPlane(wanted, report.Axis).normalized;

				SpaxDebug.Log("ARMELBOW",
					$"{(transition.Arm.IsLeft ? "LEFT" : "RIGHT")} {transition.Leg} t {progress:0.##}" +
					$" | w {weight:0.###} auth {authority:0.###}" +
					// How far the elbow ACTUALLY travelled this frame, against every term that could move
					// it. drift is how far it ended up from what the animation's swivel asked for.
					$" | TRAVELLED {Vector3.Angle(report.Was, report.Held):0.#}" +
					$" drift {Vector3.Angle(neutral, report.Held):0.#}" +
					$" | neutral {inverse * neutral}" +
					// free = limit idle, edge = riding it, RECOVER = it teleported us back inside.
					$" | limit {report.Mode} clamped {report.Clamped:0.#}" +
					// inside = how far into the body the elbow ends up; bodied = how far the body test
					// moved it. A big clamp against a small bodied means the two fought.
					$" | inside {ElbowHintSolver.BodyDepth(torso, hint):0.###}" +
					$" bodied {report.Bodied:0.#}" +
					$" | sideways {Vector3.Angle(hint - shoulder.position, outward):0.#}" +
					// 1 means the arm has run out of bend and the elbow circle has collapsed to a point.
					$" | straight {Vector3.Distance(shoulder.position, hand) / Mathf.Max(ArmLength(transition.Arm.IsLeft), 0.0001f):0.###}" +
					$" radius {report.Radius:0.###}" +
					$" | want {inverse * (hand - body.origin)}" +
					$" | hint {inverse * (hint - body.origin)}" +
					$" | elbow {inverse * (elbow.position - body.origin)}" +
					$" | shoulder {inverse * (shoulder.position - body.origin)}" +
					// The two directions on the ring: where the elbow was, and where it ended up.
					$" | was {inverse * report.Was} held {inverse * report.Held}" +
					$" | agrees {Vector3.Dot(Vector3.ProjectOnPlane(elbow.position - report.Centre, report.Axis).normalized, report.Held):0.##}");
			}
		}

		#endregion Elbow

		#region Bones

		private Transform Shoulder(ArmState arm)
		{
			return lookup.Lookup(arm.IsLeft ? HumanBoneIdentifiers.LEFT_UPPER_ARM : HumanBoneIdentifiers.RIGHT_UPPER_ARM);
		}

		private Transform Elbow(ArmState arm)
		{
			return lookup.Lookup(arm.IsLeft ? HumanBoneIdentifiers.LEFT_LOWER_ARM : HumanBoneIdentifiers.RIGHT_LOWER_ARM);
		}

		/// <summary>The clavicle — the top of the shoulder, which is what a reach has to clear.</summary>
		private Transform ShoulderTop(ArmState arm)
		{
			return lookup.Lookup(arm.IsLeft ? HumanBoneIdentifiers.LEFT_SHOULDER : HumanBoneIdentifiers.RIGHT_SHOULDER);
		}

		/// <summary>A bone's height in the torso's frame, for logging. Debug only.</summary>
		private float Height(Transform bone)
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return bone == null ? float.NaN : (Quaternion.Inverse(body.rotation) * (bone.position - body.origin)).y;
		}

		/// <summary>What an armament is and where it rests, so a leg in the log says which swap it belongs to.</summary>
		private string Name(RuntimeEquipedData data)
		{
			if (data == null)
			{
				return "nothing";
			}

			string item = data.EquipedInstance == null ? "?" : data.EquipedInstance.name;
			return $"{item}[{(data.Slot == null ? "-" : data.Slot.ID)}]";
		}

		/// <summary>Where an armament's own resting anchor sits in agent space, for logging. Debug only.</summary>
		private string SlotInfo(ArmState arm, RuntimeEquipedData data)
		{
			if (data == null)
			{
				return "-";
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return (Quaternion.Inverse(body.rotation) *
				(GetRestOrientation(arm, data).pos - body.origin)).ToString();
		}

		/// <summary>
		/// How far past the body a swing rides: the hand's own collision sphere, and nothing else. What it
		/// grips sits inside that sphere, so it has nothing of its own to clear.
		/// </summary>
		public float HandRadius(bool isLeft)
		{
			if (agentBody == null || !agentBody.TryGetBoneColliders(
				isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND,
				out IReadOnlyList<Collider> colliders))
			{
				return 0f;
			}

			for (int i = 0; i < colliders.Count; i++)
			{
				if (colliders[i] is SphereCollider sphere)
				{
					// A sphere takes the largest axis, the way Unity scales one.
					Vector3 scale = sphere.transform.lossyScale;
					return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
				}
			}

			return 0f;
		}

		#endregion Bones

		#region Pose sampling

		/// <summary>
		/// Pins a pose in the torso's frame, so it tracks the body without feeding back off the hand it
		/// drives. The inverse of <see cref="BodyPose"/>.
		/// </summary>
		private (Vector3 pos, Quaternion rot) Localize(Vector3 position, Quaternion rotation)
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Quaternion inverse = Quaternion.Inverse(body.rotation);
			return (inverse * (position - body.origin), inverse * rotation);
		}

		/// <summary>Pins where the hand hands over, so the next leg has somewhere stable to start from.</summary>
		private void Freeze(ArmTransition transition, Vector3 position, Quaternion rotation)
		{
			(transition.FrozenPosition, transition.FrozenRotation) = Localize(position, rotation);
		}

		/// <summary>
		/// Catches the hand where the animation currently has it, so a leg taking over starts exactly
		/// where the arm already is.
		/// </summary>
		private void FreezeHand(ArmTransition transition)
		{
			Transform hand = transition.Arm.IsLeft ? LeftHand : RightHand;
			Freeze(transition, hand.position, hand.rotation);
		}

		/// <summary>
		/// Where the animation has the hands, taken in LateUpdate before the solver writes, so it is the
		/// animated pose and not our own output read back. Recovery aims here, so it follows a new idle.
		/// Subscribed directly to LateUpdate by the owner — call once per frame with both arms.
		/// </summary>
		public void SampleAnimatedHands(ArmState left, ArmState right)
		{
			if (LeftHand != null)
			{
				leftAnimated = Localize(LeftHand.position, LeftHand.rotation);
				leftSwivel = AnimatedSwivel(left, LeftHand);
			}
			if (RightHand != null)
			{
				rightAnimated = Localize(RightHand.position, RightHand.rotation);
				rightSwivel = AnimatedSwivel(right, RightHand);
			}
			sampledAnimated = LeftHand != null || RightHand != null;
		}

		/// <summary>
		/// Which way the animation has this arm's elbow off its own shoulder-to-hand line, in agent space.
		/// Read here with the animated pose, before the solver writes over it.
		/// </summary>
		private (Vector3 axis, Vector3 roll) AnimatedSwivel(ArmState arm, Transform handBone)
		{
			Transform shoulder = Shoulder(arm);
			Transform elbow = Elbow(arm);
			if (shoulder == null || elbow == null)
			{
				return (Vector3.zero, Vector3.zero);
			}

			Vector3 axis = (handBone.position - shoulder.position).normalized;
			Vector3 roll = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis);
			if (axis == Vector3.zero || roll.sqrMagnitude < 0.000001f)
			{
				return (Vector3.zero, Vector3.zero);
			}

			Quaternion inverse = Quaternion.Inverse(agentTransform.rotation);
			return (inverse * axis, inverse * roll.normalized);
		}

		/// <summary>Where recovery lands: the animation's own hand, or the pose it had when claimed.</summary>
		private (Vector3 pos, Quaternion rot) AnimatedHome(ArmTransition transition)
		{
			if (!sampledAnimated)
			{
				return (transition.HomePosition, transition.HomeRotation);
			}

			return transition.Arm.IsLeft ? leftAnimated : rightAnimated;
		}

		/// <summary>The pose the animation had the hand in when this transition claimed the arm.</summary>
		private void CaptureHome(ArmTransition transition)
		{
			Transform hand = transition.Arm.IsLeft ? LeftHand : RightHand;
			(transition.HomePosition, transition.HomeRotation) = Localize(hand.position, hand.rotation);
			transition.HomeTwist = Quaternion.Inverse(agentTransform.rotation) * BodyFrame().rotation;
		}

		#endregion Pose sampling

		#region Reach

		/// <summary>
		/// Whether a grip sits over the shoulder rather than under it. Measured against the top of the
		/// shoulder, not the joint, and by a clear margin: a sheathe stack shifts a grip a centimetre or
		/// two, and that must never be what decides which way the arm comes at it.
		/// </summary>
		private bool OverShoulderGrip(ArmState arm, Vector3 grip)
		{
			Transform top = ShoulderTop(arm);
			Transform reference = top == null ? Shoulder(arm) : top;
			float reach = ArmLength(arm.IsLeft);
			if (reference == null)
			{
				return false;
			}

			Vector3 up = BodyFrame().rotation * Vector3.up;
			return Vector3.Dot(grip - reference.position, up) > reach * OVER_SHOULDER_MARGIN;
		}

		/// <summary>
		/// The way in to a grip that sits over the shoulder: up and in front of it. Anything at or below
		/// the shoulder is reached under it instead, which the orbit already does by going round the body.
		/// </summary>
		private (Vector3 axis, float depth) OverShoulder(ArmState arm, Vector3 grip)
		{
			Transform shoulder = Shoulder(arm);
			float reach = ArmLength(arm.IsLeft);
			if (shoulder == null || reach <= 0f || !OverShoulderGrip(arm, grip))
			{
				return (Vector3.zero, 0f);
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Vector3 forward = body.rotation * Vector3.forward;
			Vector3 up = body.rotation * Vector3.up;

			Vector3 gate = shoulder.position + up * (reach * OVER_SHOULDER_RISE) + forward * (reach * OVER_SHOULDER_LEAD);
			Vector3 approach = gate - grip;
			float distance = approach.magnitude;
			if (distance <= 0.0001f)
			{
				return (Vector3.zero, 0f);
			}

			// Held the way an armament's is: pointing in, so the withdraw runs back out along it.
			approach /= distance;
			return (Quaternion.Inverse(agentTransform.rotation) * -approach,
				Mathf.Min(distance, ReachLimit(arm, grip, approach)));
		}

		/// <summary>
		/// Which way an armament slides into its resting place and how far, in agent space. Capped by how
		/// far the arm actually reaches — a greatsword on the back can never come fully clear.
		/// </summary>
		private (Vector3 axis, float depth) InsertMotion(ArmState arm, RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			if (carryable == null || carryable.SheathedLength <= 0f || data.EquipedInstance == null)
			{
				return (Vector3.zero, 0f);
			}

			Quaternion restRotation;
			Vector3 anchor;
			if (sheathe != null && sheathe.TryGetSlotOrientation(data, out _, out Quaternion slotRotation))
			{
				restRotation = slotRotation;
				anchor = GetSheathingOrientation(arm, data).pos;
			}
			else
			{
				// Just drawn: still in hand at the resting place it left, so its own pose is the resting one.
				restRotation = data.EquipedInstance.transform.rotation;
				anchor = (arm.IsLeft ? LeftHand : RightHand).position;
			}

			Vector3 axis = (restRotation * carryable.InsertAxis).normalized;
			float depth = Mathf.Min(carryable.SheathedLength, ReachLimit(arm, anchor, -axis));

			return (Quaternion.Inverse(agentTransform.rotation) * axis, depth);
		}

		/// <summary>Shoulder-to-hand length of an arm, measured live so it follows rig scale.</summary>
		public float ArmLength(bool isLeft)
		{
			Transform shoulder = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_UPPER_ARM : HumanBoneIdentifiers.RIGHT_UPPER_ARM);
			Transform elbow = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_LOWER_ARM : HumanBoneIdentifiers.RIGHT_LOWER_ARM);
			Transform hand = isLeft ? LeftHand : RightHand;
			if (shoulder == null || elbow == null || hand == null)
			{
				return 0f;
			}

			return Vector3.Distance(shoulder.position, elbow.position) +
				Vector3.Distance(elbow.position, hand.position);
		}

		/// <summary>
		/// How far along <paramref name="direction"/> the hand can travel from <paramref name="origin"/>
		/// before the arm runs out. Ray against the shoulder's reach sphere.
		/// </summary>
		private float ReachLimit(ArmState arm, Vector3 origin, Vector3 direction)
		{
			Transform shoulder = lookup.Lookup(arm.IsLeft ? HumanBoneIdentifiers.LEFT_UPPER_ARM : HumanBoneIdentifiers.RIGHT_UPPER_ARM);
			float reach = ArmLength(arm.IsLeft);
			if (shoulder == null || reach <= 0f)
			{
				return float.MaxValue;
			}

			// Stop short of a locked-out arm: a slide run to the very edge of reach ends dead straight.
			// Never inside where the grip already is, or a grip further out would have no slide at all.
			Vector3 offset = origin - shoulder.position;
			reach = Mathf.Max(reach * SLIDE_REACH, offset.magnitude);

			float along = Vector3.Dot(offset, direction);
			float outside = Vector3.Dot(offset, offset) - reach * reach;
			float discriminant = along * along - outside;

			return discriminant < 0f ? 0f : Mathf.Max(-along + Mathf.Sqrt(discriminant), 0f);
		}

		private float DurationFor(float distance)
		{
			return Mathf.Max(distance / Mathf.Max(handSpeed, 0.01f), minLegDuration);
		}

		#endregion Reach

		#region Orientation and physical placement

		/// <summary>
		/// Retrieve the position and rotation of a hand slot.
		/// </summary>
		/// <param name="isLeft">Whether to retrieve for the left (true) or right hand (false).</param>
		/// <param name="local">Whether to retrieve the orientation in local space relative to the hand (true) or global space (false).</param>
		/// <param name="wieldRadius">Half the grip's thickness — how far off the palm the held object's axis sits.</param>
		/// <returns>An orientation tuple (position, rotation) of the <paramref name="isLeft"/> hand's slot in <paramref name="local"/> space.</returns>
		public (Vector3 pos, Quaternion rot) GetHandSlotOrientation(bool isLeft, bool local,
			float wieldRadius = AgentSheatheComponent.DEFAULT_WIELD_RADIUS)
		{
			// Calculate position.
			Transform hand = isLeft ? LeftHand : RightHand;
			Vector3 handPos = hand.position;
			Vector3 middleFPos = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_MIDDLE_PROXIMAL : HumanBoneIdentifiers.RIGHT_MIDDLE_PROXIMAL).position;
			Vector3 position = Vector3.Lerp(handPos, middleFPos, 0.8f);

			// Calculate rotation.
			Vector3 thumb = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_THUMB_PROXIMAL : HumanBoneIdentifiers.RIGHT_THUMB_PROXIMAL).position;
			Vector3 handToMiddleF = Vector3.Normalize(middleFPos - handPos);
			Vector3 handToThumb = Vector3.Normalize(thumb - handPos);
			Quaternion rotation = Quaternion.LookRotation(handToThumb, -handToMiddleF);

			// X is the palm normal, so the held object's axis sits one grip-radius off the palm.
			position += rotation * new Vector3(isLeft ? wieldRadius : -wieldRadius, 0f, 0f);

			if (local)
			{
				// Convert to local.
				position = hand.InverseTransformPoint(position);
				rotation = Quaternion.Inverse(hand.rotation) * rotation;
			}

			return (position, rotation);
		}

		/// <summary>Where <paramref name="subject"/>'s root rests when it is not in hand.</summary>
		private (Vector3 pos, Quaternion rot) GetRestOrientation(ArmState arm, RuntimeEquipedData subject)
		{
			if (sheathe != null && sheathe.TryGetSlotOrientation(subject, out Vector3 slotPos, out Quaternion slotRot))
			{
				return (slotPos, slotRot);
			}

			Transform fallback = arm.IsLeft ? LeftSheathe : RightSheathe;
			return (fallback.position, fallback.rotation);
		}

		/// <summary>
		/// Where the hand must be for <paramref name="subject"/>'s grip to meet its resting place.
		/// </summary>
		private (Vector3 pos, Quaternion rot) GetSheathingOrientation(ArmState arm, RuntimeEquipedData subject)
		{
			(Vector3 slotPos, Quaternion slotRot) = GetRestOrientation(arm, subject);

			// The root is the sheathe anchor, so shift to where the grip will end up.
			// Kept in world units — dividing by the root's scale would not match the hand's.
			Vector3 anchorPos = slotPos;
			Quaternion anchorRot = slotRot;
			Transform grip = ArmUtils.GripOf(subject);
			if (grip != null)
			{
				Transform root = subject.EquipedInstance.transform;
				Quaternion rootInverse = Quaternion.Inverse(root.rotation);
				anchorPos = slotPos + slotRot * (rootInverse * (grip.position - root.position));
				anchorRot = slotRot * (rootInverse * grip.rotation);
			}

			(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(arm.IsLeft, true,
				AgentSheatheComponent.WieldRadiusOf(subject));

			Transform hand = arm.IsLeft ? LeftHand : RightHand;
			orientation.pos = orientation.pos * hand.lossyScale.x;

			// The hand whose PALM lands on the grip. The palm sits at hand * local, so getting there is
			// hand = anchor * inverse(local) — composing it the other way leaves the palm turned by the
			// local frame TWICE. It is 170.8 degrees off identity, so that reads as a 18.4 degree snap.
			orientation.rot = anchorRot * Quaternion.Inverse(orientation.rot);
			orientation.pos = anchorPos - orientation.rot * orientation.pos;

			if (DrawGizmos)
			{
				Debug.DrawLine(hand.position, anchorPos, Color.yellow);
			}

			return orientation;
		}

		/// <summary>Re-sorts this arm's stowed armaments after its active slot changed.</summary>
		public void RefreshStowOrder(ArmState arm)
		{
			if (sheathe == null)
			{
				return;
			}

			for (int i = 0; i < arm.Armaments.Count; i++)
			{
				RuntimeEquipedData data = arm.Armaments[i];
				if (data != null && data != arm.Wielded)
				{
					sheathe.TryAssign(data, arm.Side, ArmUtils.StowOrder(arm, data));
				}
			}
		}

		/// <summary>Rests an armament at its sheathe point, or on the arm's plain sheathe transform.</summary>
		public void Stow(ArmState arm, RuntimeEquipedData data)
		{
			if (data == null || data.EquipedInstance == null)
			{
				return;
			}

			if (sheathe != null)
			{
				sheathe.TryAssign(data, arm.Side, ArmUtils.StowOrder(arm, data));
				LogHandover(arm, data);
				if (sheathe.Place(data))
				{
					return;
				}
			}

			Transform transform = data.EquipedInstance.transform;
			transform.SetParent(arm.IsLeft ? LeftSheathe : RightSheathe);
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
		}

		/// <summary>
		/// What the hand is about to let go of, against where the slot is about to snap it. Debug only.
		/// Separates an IK shortfall from a grip drifted off the palm from the anchor maths being wrong.
		/// </summary>
		private void LogHandover(ArmState arm, RuntimeEquipedData data)
		{
			if (!DebugLogging)
			{
				return;
			}

			Transform handBone = arm.IsLeft ? LeftHand : RightHand;
			Transform root = data.EquipedInstance.transform;
			if (root.parent != handBone)
			{
				// Never was in this hand — resting an idle armament says nothing about the hand-over.
				return;
			}

			if (!sheathe.TryGetSlotOrientation(data, out Vector3 slotPos, out Quaternion slotRot))
			{
				SpaxDebug.Log("ARMSTOW", $"{(arm.IsLeft ? "LEFT" : "RIGHT")} {Name(data)} | NO SLOT reserved.");
				return;
			}

			Transform grip = ArmUtils.GripOf(data);
			(Vector3 pos, Quaternion rot) aim = GetSheathingOrientation(arm, data);
			(Vector3 pos, Quaternion rot) palm = GetHandSlotOrientation(arm.IsLeft, false,
				AgentSheatheComponent.WieldRadiusOf(data));

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Quaternion inverse = Quaternion.Inverse(body.rotation);
			Vector3 blade = data.Carryable == null ? Vector3.forward : data.Carryable.InsertAxis;

			SpaxDebug.Log("ARMSTOW",
				$"{(arm.IsLeft ? "LEFT" : "RIGHT")} {Name(data)}" +
				// SNAP is the jump itself: the root now, against where Place is about to put it.
				$" | SNAP {(inverse * (slotPos - root.position)).ToString("F3")}" +
				$" {Quaternion.Angle(root.rotation, slotRot):0.#}deg" +
				$" | blade {(inverse * (root.rotation * blade)).ToString("F2")}" +
				$" -> {(inverse * (slotRot * blade)).ToString("F2")}" +
				// hand = what the IK failed to deliver of the pose the leg actually asked for.
				$" | hand {Vector3.Distance(handBone.position, aim.pos):0.###}" +
				$" {Quaternion.Angle(handBone.rotation, aim.rot):0.#}deg" +
				// grip = how far the item drifted off the live palm frame since Draw seated it there.
				$" | grip {(grip == null ? 0f : Vector3.Distance(grip.position, palm.pos)):0.###}" +
				$" {(grip == null ? 0f : Quaternion.Angle(grip.rotation, palm.rot)):0.#}deg" +
				$" | scale root {root.lossyScale.ToString("F3")} hand {handBone.lossyScale.ToString("F3")}");
		}

		/// <summary>Takes an armament off the body and aligns its grip to the hand.</summary>
		public void Draw(ArmState arm, RuntimeEquipedData data)
		{
			if (data == null || data.EquipedInstance == null)
			{
				return;
			}

			if (sheathe != null)
			{
				sheathe.Release(data);
			}

			(Vector3 pos, Quaternion rot) slot = GetHandSlotOrientation(arm.IsLeft, false,
				AgentSheatheComponent.WieldRadiusOf(data));

			Transform transform = data.EquipedInstance.transform;
			Transform hand = arm.IsLeft ? LeftHand : RightHand;
			Transform grip = ArmUtils.GripOf(data);

			if (DebugLogging)
			{
				// moved = how far AlignGrip turns the armament to seat it. Non-zero means the palm frame
				// the approach aimed with is not the palm frame the attach found.
				SpaxDebug.Log("ARMGRIP",
					$"{(arm.IsLeft ? "LEFT" : "RIGHT")} draw {Name(data)}" +
					$" | moved {(grip == null ? 0f : Quaternion.Angle(grip.rotation, slot.rot)):0.#}" +
					$" | palm local {(Quaternion.Inverse(hand.rotation) * slot.rot).eulerAngles}" +
					$" | hand {hand.rotation.eulerAngles} slot {slot.rot.eulerAngles}");
			}

			transform.SetParent(hand);
			ArmUtils.AlignGrip(transform, grip, slot.pos, slot.rot);
		}

		#endregion Orientation and physical placement

		#region Lifecycle and gizmos

		/// <summary>Releases both arms' IK influence — the whole-performance reset.</summary>
		public void ReleaseAll()
		{
			ik.RemoveInfluencer(owner, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(owner, IKChainConstants.RIGHT_ARM);
			ik.RemoveHintInfluencer(owner, IKChainConstants.LEFT_ARM);
			ik.RemoveHintInfluencer(owner, IKChainConstants.RIGHT_ARM);
		}

		/// <summary>
		/// The capsules the arc is actually measured against, drawn where the code reads them rather than
		/// where the collider gizmo puts them. Worth seeing whenever a hand clips something it "cleared".
		/// </summary>
		public void DrawBodyProfile()
		{
			if (lookup == null)
			{
				return;
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			BodyCapsule[] capsules = BodyCapsules();
			Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);

			for (int i = 0; i < capsules.Length; i++)
			{
				Vector3 start = body.origin + body.rotation * capsules[i].Start;
				Vector3 end = body.origin + body.rotation * capsules[i].End;
				Gizmos.DrawWireSphere(start, capsules[i].Radius);
				Gizmos.DrawWireSphere(end, capsules[i].Radius);
				Gizmos.DrawLine(start, end);
			}
		}

		/// <summary>Where each hand slot sits right now, and the frame it is aimed with.</summary>
		public void DrawHandSlotGizmos()
		{
			if (lookup == null)
			{
				return;
			}

			Draw(true);
			Draw(false);

			void Draw(bool isLeft, float size = 0.3f)
			{
				(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(isLeft, false);
				Gizmos.color = Color.yellow;
				Gizmos.DrawSphere(orientation.pos, 0.02f);
				Gizmos.color = Color.blue;
				Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.forward * size);
				Gizmos.color = Color.red;
				Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * (isLeft ? Vector3.left : Vector3.right) * size);
				Gizmos.color = Color.green;
				Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.up * size);
			}
		}

		#endregion Lifecycle and gizmos
	}
}
