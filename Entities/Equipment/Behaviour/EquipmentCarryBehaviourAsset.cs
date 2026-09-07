using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Carries a single-handed armament as a weight on a compliant grip under apparent gravity. Its position
	/// chases in the BODY'S frame and its rotation in the WORLD'S — see the frames note in OnUpdate.
	/// </summary>
	[CreateAssetMenu(fileName = "EquipmentCarryBehaviourAsset", menuName = "ScriptableObjects/Behaviours/EquipmentCarryBehaviourAsset")]
	public class EquipmentCarryBehaviourAsset : BehaviourAsset
	{
		[SerializeField, Tooltip("Gizmos, the setup dump, and a line per frame the blade is clipping something.")]
		private bool debug;
		[SerializeField, Conditional(nameof(debug)), Tooltip("Adds the whole carry model, every frame, plus a rollup each second. Loud.")]
		private bool debugVerbose;
		[SerializeField, Tooltip("IK priority of the carry. Outranked by the arms component's draw/sheathe legs.")]
		private int ikPrio = 1;

		[Header("Weight")]
		[SerializeField, Tooltip("Seconds of lag at a wield ratio of 1, where the limb being carried exactly matches the strength holding it.")]
		private float smoothTime = 0.5f;
		[SerializeField, Tooltip("Scales the lag for movement only. Below 1 the grip keeps up better than it turns.")]
		private float posTimeMult = 0.75f;
		[SerializeField, Tooltip("Scales the lag for turning only. Below 1 the grip turns better than it keeps up.")]
		private float rotTimeMult = 1f;
		[SerializeField, Min(0.01f), Tooltip("Gyration radius that turns as easily as its mass alone suggests. Longer armaments resist by the square of their ratio to this.")]
		private float referenceGyration = 0.5f;
		[SerializeField, Range(0f, 90f), Tooltip("Most the weight may nose an armament down, reached as the wield ratio falls to nothing. Struggling is not the same as losing hold of it.")]
		private float maxSagAngle = 60f;

		[Header("Limits")]
		[SerializeField, Tooltip("How far the grip may trail the animation, as a fraction of arm length. An asymptote, not a cap: the trail approaches it and never arrives.")]
		private float maxLagFraction = 0.7f;
		[SerializeField, Tooltip("How far the armament may trail the animation in degrees. Independent of the distance above, and an asymptote in the same way.")]
		private float maxLagAngle = 45f;
		[SerializeField, Range(0.5f, 1f), Tooltip("How much of the arm's length the hand goal may sit from the shoulder. Under 1 so a drag never straightens the arm or drags the shoulder after it.")]
		private float reachFraction = 0.98f;
		[SerializeField, Min(0f), Tooltip("Single-frame movement beyond what velocity explains that counts as a teleport, snapping the carry instead of dragging it.")]
		private float teleportDistance = 1f;

		[Header("Avoidance")]
		[SerializeField, Tooltip("What the blade steers out of. The carrying body is always excluded, so Bumper here means other agents only.")]
		private LayerMask avoidMask;
		[SerializeField, Min(0f), Tooltip("How far clear of a surface the blade is held, both steering the tip out of the world and yawing the whole blade off the body.")]
		private float clearance = 0.05f;
		[SerializeField, Range(1, 9), Tooltip("Rays sampling an obstacle's shape. The centre one finds the clip; the rest only vote on which way out.")]
		private int avoidRays = 5;
		[SerializeField, Range(0f, 60f), Tooltip("How wide the fan spreads, vertically. Wide enough to straddle a step's riser and tread at carrying distance.")]
		private float avoidSpread = 25f;
		[SerializeField, Range(0f, 180f), Tooltip("Most the armament may be turned out of an obstacle. Wide, so a blade in a bottom corner can pitch right up out of it.")]
		private float maxAvoidAngle = 120f;
		[SerializeField, Min(0f), Tooltip("Seconds to steer clear of a clip. Kept near zero: clipping reads far worse than a quick correction. 0 is immediate.")]
		private float avoidResponse = 0.04f;
		[SerializeField, Range(0f, 1f), Tooltip("How strongly up breaks a tie when surfaces disagree which way is out. Small: it only decides when nothing else can, as in a bottom corner.")]
		private float upBias = 0.2f;
		[SerializeField, Min(0f), Tooltip("Seconds to settle on which way out. Stairs change which faces the fan samples every frame; this holds the direction still while the depth stays instant.")]
		private float surfaceSmoothTime = 0.15f;

		[Header("Aim")]
		[SerializeField, Tooltip("Degrees the armament may point away from the body's forward before the carry starts correcting it.")]
		private float smoothMaxAngle = 20f;
		[SerializeField, Tooltip("Degrees the armament's aim asymptotes towards. It eases in from the angle above and never quite reaches this one.")]
		private float absoluteMaxAngle = 45f;
		[SerializeField, Range(0f, 90f), Tooltip("Degrees the tip may sit above horizontal. Held tighter than the forward pull, since pointing at the sky reads worse than pointing across the body.")]
		private float maxPitchAngle = 10f;
		[SerializeField, Range(0f, 1f), Tooltip("How much of the aim correction to apply at full speed. 0 follows the authored poses exactly, however crooked they aim.")]
		private float forwardCorrection = 1f;
		[SerializeField, Min(0f), Tooltip("Speed at which the tip starts being brought forward. Below this the authored pose is left alone.")]
		private float aimMinSpeed = 0.5f;
		[SerializeField, Min(0.01f), Tooltip("Speed at which the tip is brought forward in full.")]
		private float aimMaxSpeed = 3f;

		private RuntimeEquipedData equipedData;
		private IAgent agent;
		private AgentArmsComponent arms;
		private IIKComponent ik;
		private TransformLookup lookup;
		private CallbackService callbackService;
		private CombatSettings combatSettings;
		private GrounderComponent grounder;
		private EntityStat timescale;
		private EntityStat limbMassStat;

		private bool initialized;
		private bool isLeft;
		private Transform hand;
		private Transform shoulder;
		private string ikChain;

		// The follower: where the weight has actually got to, and how fast it is going.
		private Vector3 gripPos;
		private Vector3 posVelocity;
		private Quaternion gripRot;
		private Vector3 angularVelocity;
		private bool settled;
		private Vector3 lastAgentPos;

		// What the last cast decided: which way out of an obstacle, and how far. Held between casts, which
		// run on the entity's own optimized cadence rather than every frame.
		private Vector3 avoidEscape;
		private Vector3 avoidNormal;
		private float avoidTarget;
		private float avoidAngle;
		private Vector3 avoidGrip;
		private Vector3 avoidTip;
		private bool avoidPosed;

		// Scratch for the self-filtered casts, so probing every frame allocates nothing.
		private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

		private Vector3 debugTarget;
		private float debugMaxLag;

		// The positional follower is body-local, so the lag IS a body-frame vector — nothing to convert.
		private Vector3 debugGripWorld, debugTargetLocal, debugLag;
		private float debugRotLag;

		// Diagnostics, written where they are cheap and only read under `debug`.
		private float debugPosTime, debugRotTime, debugScale, debugRotScale, debugPosExt, debugRotExt;
		private float debugAim, debugAimRamp, debugAimFix, debugAimPitch;
		private float debugPushed, debugReached, debugDroop, debugHang, debugAccel, debugBodyTurn, debugReachRatio;
		private bool debugSnapped, debugTeleported, debugLogged;
		private Vector3 debugHandGoal, debugTip;
		private float debugTimer, debugSumLag, debugPeakLag, debugSumRotLag, debugPeakRotLag;
		private float debugSumDroop, debugPeakDroop, debugSumExt, debugPeakExt, debugSumSpeed;
		private float debugPeakAvoid, debugAvoidHit = -1f;
		private Vector3 debugAvoidNormal;
		private int debugFrames, debugSnaps, debugLifts, debugPushes, debugReaches;

		public void InjectDependencies(RuntimeEquipedData equipedData, IAgent agent,
			AgentArmsComponent arms, TransformLookup lookup, CallbackService callbackService,
			CombatSettings combatSettings,
			[Optional] IIKComponent ik, [Optional] GrounderComponent grounder)
		{
			this.equipedData = equipedData;
			this.agent = agent;
			this.arms = arms;
			this.ik = ik;
			this.lookup = lookup;
			this.callbackService = callbackService;
			this.combatSettings = combatSettings;
			this.grounder = grounder;

			initialized = ik != null;

			timescale = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(
				equipedData.Slot.Type == EquipmentSlotTypes.LEFT_HAND
					? AgentStatIdentifiers.SUB_LEFT_HAND
					: AgentStatIdentifiers.SUB_RIGHT_HAND));
		}

		public override void Start()
		{
			base.Start();

			if (!initialized)
			{
				return;
			}

			isLeft = equipedData.Slot.Type == EquipmentSlotTypes.LEFT_HAND;
			hand = isLeft ? arms.LeftHand : arms.RightHand;
			shoulder = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_UPPER_ARM : HumanBoneIdentifiers.RIGHT_UPPER_ARM);
			ikChain = isLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;
			settled = false;
			debugPeakAvoid = 0f;

			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, OnUpdate);
			callbackService.DrawGizmosCallback += OnDrawGizmos;
			arms.SheathedEvent += OnSheathedEvent;
			agent.SubscribeOptimizedUpdate(Avoid);
		}

		public override void Stop()
		{
			base.Stop();

			if (!initialized)
			{
				return;
			}

			callbackService.UnsubscribeUpdates(this);
			arms.SheathedEvent -= OnSheathedEvent;
			callbackService.DrawGizmosCallback -= OnDrawGizmos;
			agent.UnsubscribeOptimizedUpdate(Avoid);

			Snap(Vector3.zero, Quaternion.identity);
			ik.RemoveInfluencer(this, ikChain);
			ik.RemoveHintInfluencer(this, ikChain);
		}

		public void OnUpdate(float delta)
		{
			if (arms.Sheathed)
			{
				return;
			}

			delta *= timescale;
			if (delta <= 0f)
			{
				return;
			}

			ICarryableItem carryable = equipedData.Carryable;

			// GATHER: the animated grip, with the hand and the armament's own geometry rigid to it.
			(Vector3 pos, Quaternion rot) slot = arms.GetHandSlotOrientation(isLeft, false,
				AgentSheatheComponent.WieldRadiusOf(equipedData));
			Vector3 handOffset = slot.rot.Inverse() * (hand.position - slot.pos);
			Quaternion rotationOffset = slot.rot.Inverse() * hand.rotation;
			ReadGeometry(carryable, out Vector3 aimLocal, out Vector3 tipLocal, out Vector3 comLocal);

			// CORRECT THE TARGET: running points the tip forward, so the lag chases a sane pose.
			Vector3 targetPos = slot.pos;
			Quaternion targetRot = CorrectAim(slot.rot, aimLocal, carryable != null && carryable.HasAim);

			// WEIGHT: how well the body wields the limb it is swinging, weapon mass and all — the same
			// ratio and the same curve the melee performer runs on, so a weapon that is sluggish to swing
			// is sluggish to carry. Under 1 the armament outweighs the strength holding it.
			float wieldRatio = WieldRatio();
			float wield = Mathf.Max(combatSettings.WieldSpeedFactor(wieldRatio), 0.01f);
			float gyration = Mathf.Max(carryable == null ? referenceGyration : carryable.GyrationRadius, 0.01f);

			// Lag is the reciprocal of that speed: what swings slowly, follows slowly. Turning resists by
			// the square of the lever on top, so a long blade is far more ponderous than its mass says.
			float posTime = Mathf.Max(smoothTime * posTimeMult / wield, delta);
			float rotTime = Mathf.Max(smoothTime * rotTimeMult * Leverage(gyration) / wield, delta);
			float armLength = arms.ArmLength(isLeft);
			float maxLag = armLength * maxLagFraction;
			float pull = grounder == null ? Mathf.Abs(Physics.gravity.y) : grounder.Gravity.Value;
			Vector3 gravity = Vector3.down * pull;

			// A body that jumped in space never dragged the weapon there, so there is nothing to catch up on.
			// Measured against what its own velocity explains, so merely falling fast is never a teleport.
			Vector3 agentPos = agent.Transform.position;
			float plausible = Speed() * delta + teleportDistance;
			bool teleported = settled &&
				Vector3.SqrMagnitude(agentPos - lastAgentPos) > plausible * plausible;
			bool snapped = !settled || teleported;
			lastAgentPos = agentPos;
			debugTeleported = teleported;
			debugSnapped = snapped;

			// THE TWO HALVES LIVE IN DIFFERENT FRAMES, and deliberately so.
			// POSITION is body-local: travelling at a steady speed takes no force, so any trail it produces is
			// pure tracking artefact — and a large one, since it grows with speed and not with the weight.
			// ROTATION stays in the world: swinging an armament around a turn DOES take force, applied at a
			// grip that is not its centre of mass, so a real weapon does trail — which is what `rotTime` and
			// Leverage(gyration) exist to express, and they have nothing to act on in a frame that turns.
			Quaternion agentRot = agent.Transform.rotation;
			Quaternion toLocal = agentRot.Inverse();
			Vector3 targetLocal = toLocal * (targetPos - agentPos);
			Vector3 gravityLocal = toLocal * gravity;

			// Being carried costs nothing, but being ACCELERATED does: that is the force the hand really has
			// to put through the grip, so it is what the weight trails on. Starting to run drags the armament
			// back, stopping throws it forward, and a steady cruise leaves it be. Kept in both frames — the
			// chase integrates it body-local, the sag torques the armament about a world axis.
			Vector3 apparent = gravity - BodyAcceleration();
			Vector3 apparentLocal = toLocal * apparent;

			// Gravity is in the follower ONLY so a fall carries the armament down with the body instead of
			// leaving it overhead. The hang it settles into is a side effect, not the point — so where the
			// weight WOULD rest is worked out and taken back out, leaving lag that is purely being dragged.
			// It goes to nothing in free fall, where the body is already falling as fast as the weight.
			float falling = Mathf.Clamp01(Vector3.Dot(BodyAcceleration(), gravity) / gravity.sqrMagnitude);
			Vector3 hang = gravityLocal * ((1f - falling) * posTime * posTime * 0.25f);
			debugHang = hang.magnitude;

			if (snapped)
			{
				Snap(targetLocal + hang, targetRot);
				debugPosExt = debugRotExt = debugScale = debugRotScale = 0f;
			}
			else
			{
				Follow(targetLocal, targetRot, apparentLocal, apparent, comLocal, wield, posTime, rotTime, delta);
				// Measured from where the weight RESTS, so the hang never counts against the drag's room.
				Saturate(targetLocal + hang, targetRot, maxLag, maxLagAngle);
			}

			settled = true;
			debugPosTime = posTime;
			debugRotTime = rotTime;
			debugAccel = BodyAcceleration().magnitude;

			// APPLY: the follower carries the grip, the hand hangs off it. Back into the world, where every
			// guard below and the IK itself live.
			ApplyElbowHint(0.5f * arms.Weight);
			float animated = arms.Weight.Value.Invert();
			// The hang comes back out here: what is left is the drag, which is the part worth seeing.
			Vector3 outPos = (agentPos + agentRot * (gripPos - hang)).Lerp(targetPos, animated);
			Quaternion outRot = gripRot.Slerp(targetRot, animated);

			// What the next cast will measure: the pose BEFORE steering, so the correction is always read
			// off the authored blade. Feeding it the corrected one would clear the ray and undo itself.
			avoidGrip = outPos;
			avoidTip = outPos + outRot * tipLocal;
			avoidPosed = true;

			// Deliberately NOT the armament's turning rate: a blade left inside a wall reads worse than one
			// that corrects faster than its weight says it should.
			avoidAngle = avoidResponse <= 0.0001f
				? avoidTarget
				: Mathf.Lerp(avoidAngle, avoidTarget, 1f - Mathf.Exp(-delta / avoidResponse));
			outRot = SteerClear(outRot, tipLocal, avoidAngle);

			// A check on the STEERING alone, not on the pose: the authored carry keeps itself off the body,
			// and it is the escape from a wall that has no reason not to shove the blade straight through.
			debugBodyTurn = 0f;
			if (avoidAngle > 0.01f)
			{
				outRot = ClearBody(outRot, tipLocal, outPos, out debugBodyTurn);
			}

			Vector3 handGoal = KeepClear(outPos + outRot * handOffset, arms.HandRadius(isLeft), out debugPushed);
			handGoal = ClampReach(handGoal, armLength, out debugReached);

			ik.AddInfluencer(this, ikChain, ikPrio,
				handGoal, arms.Weight,
				outRot * rotationOffset, arms.Weight);

			debugTarget = targetPos;
			debugMaxLag = maxLag;
			debugHandGoal = handGoal;
			debugTip = outPos + outRot * tipLocal;
			debugGripWorld = agentPos + agentRot * gripPos;
			debugTargetLocal = targetLocal;
			debugLag = gripPos - targetLocal;
			debugRotLag = Quaternion.Angle(gripRot, targetRot);
			debugDroop = Vector3.Dot(agentRot * -debugLag, Vector3.up);

			if (debug)
			{
				LogFrame(targetPos, targetRot, slot.rot, aimLocal, carryable, wieldRatio, wield, gyration, pull, armLength, maxLag, delta);
			}
		}

		/// <summary>
		/// The armament's aim, tip and weight in the grip's own frame. Read off the item against its own
		/// grip, never against the world, so what our IK did to it last frame cannot feed back in here.
		/// </summary>
		private void ReadGeometry(ICarryableItem carryable, out Vector3 aim, out Vector3 tip, out Vector3 com)
		{
			aim = Vector3.forward;
			tip = Vector3.zero;
			com = Vector3.zero;
			if (carryable == null)
			{
				return;
			}

			Quaternion inverse = carryable.Grip.rotation.Inverse();
			Vector3 grip = carryable.GripPosition;
			aim = inverse * carryable.AimDirection;
			tip = carryable.Tip == null ? Vector3.zero : inverse * (carryable.Tip.position - grip);
			com = inverse * (carryable.CenterOfMass - grip);
		}

		/// <summary>
		/// Chases the animated grip as a weight on a compliant hold. Position is body-local and rotation is
		/// world — see the frames note at the call site. Both answer to apparent gravity: gravity plus what
		/// the body's own acceleration adds to it, which is what a carried weight actually feels.
		/// </summary>
		private void Follow(Vector3 targetPos, Quaternion targetRot, Vector3 apparentLocal, Vector3 apparent,
			Vector3 comLocal, float wield, float posTime, float rotTime, float delta)
		{
			posVelocity += apparentLocal * delta;
			gripPos = gripPos.SmoothDamp(targetPos, ref posVelocity, posTime, delta);

			// The weight noses the armament down. WHICH WAY is the weight's business — where the mass sits
			// relative to the grip. HOW FAR is the carrier's: it comes from the wield ratio, so an armament heavier
			// than the body can lift keeps getting harder to hold level without ever coming off its hinge.
			Vector3 axis = Vector3.Cross(gripRot * comLocal, apparent);
			float sag = maxSagAngle * Mathf.Clamp01(1f - wield);
			gripRot = SmoothRotation(gripRot, Sag(targetRot, axis, sag), ref angularVelocity, rotTime, delta);
		}

		/// <summary>Where the hold settles the armament once the weight has had its way with it.</summary>
		private static Quaternion Sag(Quaternion targetRot, Vector3 axis, float degrees)
		{
			return degrees < 0.01f || axis.sqrMagnitude < 0.0000001f
				? targetRot
				: Quaternion.AngleAxis(degrees, axis.normalized) * targetRot;
		}

		/// <summary>
		/// <see cref="Vector3.SmoothDamp"/>'s own solution, run on the rotation error. Closed form, so it
		/// holds at any lag time — integrating the spring by hand diverges once that nears a frame.
		/// </summary>
		private static Quaternion SmoothRotation(Quaternion current, Quaternion target,
			ref Vector3 velocity, float smoothTime, float delta)
		{
			float omega = 2f / Mathf.Max(smoothTime, 0.0001f);
			float x = omega * delta;
			float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

			Vector3 error = RotationError(target, current);
			Vector3 step = (velocity + error * omega) * delta;
			velocity = (velocity - step * omega) * exp;

			return Twist(target, (error + step) * exp);
		}

		/// <summary>
		/// A leash on each half, INDEPENDENTLY. They are separate degrees of freedom and nothing physical
		/// ties them: sharing one budget only let a saturated half quietly crush the other.
		/// The arm's real limits are <see cref="ClampReach"/> and <see cref="KeepClear"/>, on real geometry.
		/// </summary>
		private void Saturate(Vector3 targetPos, Quaternion targetRot, float maxLag, float maxAngle)
		{
			Vector3 lag = gripPos - targetPos;
			float distance = lag.magnitude;
			float angle = Quaternion.Angle(gripRot, targetRot);

			if (maxLag <= 0.0001f || maxAngle <= 0.01f)
			{
				Snap(targetPos, targetRot);
				debugPosExt = debugRotExt = debugScale = debugRotScale = 0f;
				return;
			}

			debugPosExt = distance / maxLag;
			debugRotExt = angle / maxAngle;

			debugScale = Leash(debugPosExt, out float slope);
			if (distance > 0.0001f && debugScale < 0.9999f)
			{
				gripPos = targetPos + lag * debugScale;
				Bleed(ref posVelocity, lag / distance, slope);
			}

			debugRotScale = Leash(debugRotExt, out float rotSlope);
			if (angle > 0.01f && debugRotScale < 0.9999f)
			{
				Vector3 outward = RotationError(targetRot, gripRot);
				gripRot = Quaternion.RotateTowards(targetRot, gripRot, angle * debugRotScale);
				if (outward.sqrMagnitude > 0.0000001f)
				{
					Bleed(ref angularVelocity, outward.normalized, rotSlope);
				}
			}
		}

		/// <summary>
		/// How much of an overreach to keep. tanh bends it towards 1 without ever arriving, so the limit is
		/// an asymptote and nothing ever snaps taut. <paramref name="slope"/> is how much room is left.
		/// </summary>
		private static float Leash(float extension, out float slope)
		{
			if (extension < 0.0001f)
			{
				slope = 1f;
				return 1f;
			}

			float saturated = (float)Math.Tanh(extension);
			slope = 1f - saturated * saturated;
			return saturated / extension;
		}

		/// <summary>Fades out whatever is still driving the lag outwards, by however far the limit has bent it.</summary>
		private static void Bleed(ref Vector3 velocity, Vector3 direction, float slope)
		{
			float along = Vector3.Dot(velocity, direction);
			if (along > 0f)
			{
				velocity -= direction * (along * (1f - slope));
			}
		}

		/// <summary>
		/// Aims the armament back towards the body's forward once it strays, easing in over the angle band
		/// and ramping in with speed — standing still keeps the pose the animation authored.
		/// </summary>
		private Quaternion CorrectAim(Quaternion rotation, Vector3 aimLocal, bool aims)
		{
			debugAim = debugAimRamp = debugAimFix = debugAimPitch = 0f;
			if (!aims)
			{
				// A shield has a line but nothing to point along it, so its pose is left as authored.
				return rotation;
			}

			Vector3 aim = rotation * aimLocal;
			Vector3 forward = agent.Transform.forward;
			float angle = Vector3.Angle(aim, forward);
			float band = absoluteMaxAngle - smoothMaxAngle;
			float strength = forwardCorrection *
				Mathf.InverseLerp(aimMinSpeed, aimMaxSpeed, Velocity().FlattenY().magnitude);

			debugAim = angle;
			debugAimRamp = strength;
			if (strength <= 0f)
			{
				return rotation;
			}

			if (band > 0f && angle > smoothMaxAngle)
			{
				// Soft shoulder: free up to smoothMaxAngle, asymptoting to absoluteMaxAngle beyond it.
				float allowed = smoothMaxAngle + band * (1f - Mathf.Exp(-(angle - smoothMaxAngle) / band));
				debugAimFix = angle - Mathf.Lerp(angle, allowed, strength);

				// Turned dead away from forward there is no plane to turn in, so the body's own up decides.
				Vector3 axis = Vector3.Cross(aim, forward);
				axis = axis.sqrMagnitude < 0.000001f ? agent.Transform.up : axis.normalized;
				rotation = Quaternion.AngleAxis(debugAimFix, axis) * rotation;
				aim = rotation * aimLocal;
			}

			// Pointing at the sky reads far worse than pointing across the body, so elevation is held to
			// its own tighter limit on top of the forward pull.
			Vector3 pitchAxis = Vector3.Cross(Vector3.up, aim);
			float elevation = 90f - Vector3.Angle(aim, Vector3.up);
			if (elevation > maxPitchAngle && pitchAxis.sqrMagnitude > 0.000001f)
			{
				debugAimPitch = (elevation - maxPitchAngle) * strength;
				rotation = Quaternion.AngleAxis(debugAimPitch, pitchAxis.normalized) * rotation;
			}

			return rotation;
		}

		/// <summary>
		/// Decides how to steer the armament out of whatever it is inside. The centre ray alone says the
		/// blade is clipping; the rest only average an obstacle's shape, so a staircase reads as the slope
		/// it is rather than as the risers and treads it alternates between.
		/// Runs on the entity's optimized cadence, so what it decides is held until the next one.
		/// </summary>
		private void Avoid(float delta)
		{
			// Out of view is out of mind: a culled agent casts nothing and carries nothing over.
			if (agent.Priority == PriorityLevel.Culled || arms.Sheathed || !avoidPosed)
			{
				avoidTarget = 0f;
				avoidAngle = 0f;
				avoidNormal = Vector3.zero;
				avoidEscape = Vector3.zero;
				return;
			}

			Vector3 blade = avoidTip - avoidGrip;
			float length = blade.magnitude;
			if (length < 0.0001f || !CastPast(blade / length, length, out RaycastHit centre))
			{
				avoidTarget = 0f;
				debugAvoidHit = -1f;
				return;
			}

			// Blocked, so the blade IS inside something and the whole clip has to come out. The fan only
			// votes on which way; how far is the centre hit's business alone.
			FanSurface(blade / length, length, centre, out Vector3 rawNormal, out Vector3 rawEscape);
			if (rawEscape.sqrMagnitude < 0.5f)
			{
				// Straight down the blade with nothing to turn about — nothing sane to do.
				avoidTarget = 0f;
				return;
			}

			// The SURFACE is what gets smoothed, never the correction. On stairs the fan's mix of tread and
			// riser hits changes every frame, so the raw average jumps between discrete ratios and takes the
			// escape with it. Settling the estimate stops the dance; the depth stays instant, so nothing clips.
			float settle = surfaceSmoothTime <= 0.0001f ? 1f : 1f - Mathf.Exp(-delta / surfaceSmoothTime);
			Vector3 normal = avoidNormal.sqrMagnitude < 0.5f
				? rawNormal
				: Vector3.Slerp(avoidNormal, rawNormal, settle).normalized;
			Vector3 escape = avoidEscape.sqrMagnitude < 0.5f
				? rawEscape
				: Vector3.Slerp(avoidEscape, rawEscape, settle).normalized;
			avoidNormal = normal;

			// How far the tip must turn to sit clear of the surface the obstacle presents. Measured against
			// the NORMAL, not the escape: the escape is perpendicular to the blade and the hit lies on it,
			// so measuring along that always reads zero depth however deep the blade actually is.
			debugAvoidNormal = normal;
			debugAvoidHit = centre.distance / length;
			float above = Vector3.Dot(avoidGrip - centre.point, normal);
			float from = Mathf.Asin(Mathf.Clamp(Vector3.Dot(blade, normal) / length, -1f, 1f));
			float to = Mathf.Asin(Mathf.Clamp((clearance - above) / length, -1f, 1f));
			avoidEscape = escape;
			avoidTarget = Mathf.Clamp((to - from) * Mathf.Rad2Deg, 0f, maxAvoidAngle);
		}

		/// <summary>
		/// The obstacle's shape, averaged over a fan spread vertically about the blade. Averaging is what
		/// turns a staircase's alternating faces into one slope, which is the only stable thing to steer off.
		/// </summary>
		private void FanSurface(Vector3 direction, float length, RaycastHit centre,
			out Vector3 normal, out Vector3 escape)
		{
			Vector3 normals = centre.normal;
			Vector3 escapes = Perpendicular(centre.normal, direction);
			int hits = 1;

			Vector3 axis = Vector3.Cross(direction, Vector3.up);
			if (avoidRays >= 2 && axis.sqrMagnitude > 0.0000001f)
			{
				axis.Normalize();
				for (int i = 1; i < avoidRays; i++)
				{
					// Alternating either side of the blade, widening as it goes.
					float step = (i + 1) / 2 * (avoidSpread / (avoidRays / 2));
					Vector3 spread = Quaternion.AngleAxis(i % 2 == 0 ? step : -step, axis) * direction;
					if (CastPast(spread, length, out RaycastHit hit))
					{
						normals += hit.normal;
						// Projected BEFORE averaging: each surface's own escape is well defined, where the
						// raw normals of a wedged blade cancel into a direction that is pure noise.
						escapes += Perpendicular(hit.normal, direction);
					}
					else
					{
						// A ray that hits NOTHING is the only direct evidence of free space there is, and it
						// costs nothing — it was already cast. A corner clears overhead and votes up; a tall
						// wall blocks every ray and rightly leaves the escape running along its face.
						escapes += Perpendicular(spread, direction).normalized;
					}
					hits++;
				}
			}

			normal = normals.sqrMagnitude < 0.0000001f ? centre.normal : normals.normalized;

			// Up breaks the tie. Weak enough that agreeing surfaces still steer the blade sideways, and all
			// that survives when they cancel — which is exactly what a bottom corner does to them.
			escape = escapes / hits + Perpendicular(Vector3.up, direction) * upBias;

			// Never steer DOWNWARD to escape. A wall the blade points into asks to be left by retracting,
			// which for a blade already angled down means burying it in the floor — and in a corner the wall
			// outvotes the floor whenever more rays land on it. The ground is the one obstacle always there.
			float sinking = Vector3.Dot(escape, Vector3.down);
			if (sinking > 0f)
			{
				escape += Vector3.up * sinking;
			}

			escape = escape.sqrMagnitude < 0.0000001f ? Vector3.zero : escape.normalized;
		}

		/// <summary>The part of a direction that survives perpendicular to the blade — all turning can act on.</summary>
		private static Vector3 Perpendicular(Vector3 direction, Vector3 blade)
		{
			return direction - blade * Vector3.Dot(direction, blade);
		}

		/// <summary>
		/// Nearest hit that is not part of the carrying body. The ray starts at the hand, so this body's own
		/// bumper and the armament itself sit in front of everything else — a plain cast returns one of those
		/// and never sees what is past it. Gathering and filtering is what makes Bumper usable in the mask.
		/// </summary>
		private bool CastPast(Vector3 direction, float distance, out RaycastHit nearest)
		{
			nearest = default;
			int count = Physics.RaycastNonAlloc(avoidGrip, direction, hitBuffer, distance, avoidMask,
				QueryTriggerInteraction.Ignore);

			Transform root = agent.Transform.root;
			bool found = false;
			for (int i = 0; i < count; i++)
			{
				if (hitBuffer[i].transform.root == root ||
					(found && hitBuffer[i].distance >= nearest.distance))
				{
					continue;
				}

				nearest = hitBuffer[i];
				found = true;
			}

			return found;
		}

		/// <summary>
		/// Yaws the armament out of the body's own capsules, flattened, so the blade is never lying through
		/// the torso. Where it may point otherwise is nobody's business: backwards on its own side is fine,
		/// across the chest is fine once the hand is far enough forward to clear it.
		/// </summary>
		private Quaternion ClearBody(Quaternion outRot, Vector3 tipLocal, Vector3 grip, out float turned)
		{
			turned = 0f;
			ElbowHintSolver.Torso torso = arms.Torso();
			if (torso.Capsules == null || torso.Capsules.Length == 0 || tipLocal.sqrMagnitude < 0.0000001f)
			{
				return outRot;
			}

			Quaternion inverse = torso.Rotation.Inverse();
			Vector3 local = inverse * (grip - torso.Origin);
			Vector3 blade = inverse * (outRot * tipLocal);
			if (!Crosses(torso.Capsules, local, blade, 0f))
			{
				return outRot;
			}

			// Which way is out: whichever immediately carries the tip further from the body's axis. Turning
			// about the grip means the tip's own arm is the lever, so the sign falls straight out of it.
			Vector3 flat = (local + blade).FlattenY();
			float toward = Mathf.Sign(Vector3.Dot(flat, Vector3.Cross(Vector3.up, blade.FlattenY())));

			// Outward from where the blade already is, so the first clear yaw is the nearest one and is
			// reached without ever sweeping the blade through the body to get to it.
			for (float step = ArmUtils.CLEAR_STEP; step <= 180f; step += ArmUtils.CLEAR_STEP)
			{
				for (int side = 0; side < 2; side++)
				{
					float turn = step * (side == 0 ? toward : -toward);
					if (Crosses(torso.Capsules, local, blade, turn))
					{
						continue;
					}

					// Onto the boundary itself, so it slides as the body turns rather than stepping.
					float inside = turn - Mathf.Sign(turn) * ArmUtils.CLEAR_STEP;
					for (int i = 0; i < ArmUtils.CLEAR_BISECTIONS; i++)
					{
						float mid = (inside + turn) * 0.5f;
						if (Crosses(torso.Capsules, local, blade, mid))
						{
							inside = mid;
						}
						else
						{
							turn = mid;
						}
					}

					turned = turn;
					return Quaternion.AngleAxis(turn, torso.Rotation * Vector3.up) * outRot;
				}
			}

			// Nowhere at all to put it, which takes the grip itself being inside the body. Leave it be.
			return outRot;
		}

		/// <summary>Whether the blade, yawed by <paramref name="turn"/> about its grip, lies through the body.</summary>
		private bool Crosses(BodyCapsule[] capsules, Vector3 grip, Vector3 blade, float turn)
		{
			Vector3 turned = Quaternion.AngleAxis(turn, Vector3.up) * blade;
			return ArmUtils.CrossesBody(capsules, grip, grip + turned, clearance);
		}

		/// <summary>Turns the armament out of an obstacle by however far the last cast asked for.</summary>
		private Quaternion SteerClear(Quaternion outRot, Vector3 tipLocal, float angle)
		{
			Vector3 tip = outRot * tipLocal;
			Vector3 axis = Vector3.Cross(tip, avoidEscape);
			return angle < 0.01f || tip.sqrMagnitude < 0.0000001f || axis.sqrMagnitude < 0.0000001f
				? outRot
				: Quaternion.AngleAxis(angle, axis.normalized) * outRot;
		}

		/// <summary>
		/// Pushes the hand goal out of the body's own collision capsules, by the hand's own girth — the
		/// same shape and margin the arms use to swing around the torso.
		/// </summary>
		private Vector3 KeepClear(Vector3 goal, float margin, out float pushed)
		{
			pushed = 0f;
			ElbowHintSolver.Torso torso = arms.Torso();
			if (torso.Capsules == null || torso.Capsules.Length == 0)
			{
				return goal;
			}

			Vector3 local = torso.Rotation.Inverse() * (goal - torso.Origin);
			for (int i = 0; i < torso.Capsules.Length; i++)
			{
				Vector3 closest = torso.Capsules[i].Closest(local);
				Vector3 outward = local - closest;
				float distance = outward.magnitude;
				float clear = torso.Capsules[i].Radius + margin;
				if (distance >= clear)
				{
					continue;
				}

				// Dead on the axis there is no outward to speak of, so leave by this arm's own side.
				Vector3 normal = distance < 0.0001f ?
					(isLeft ? Vector3.left : Vector3.right) :
					outward / distance;
				local = closest + normal * clear;
				pushed = Mathf.Max(pushed, clear - distance);
			}

			return torso.Origin + torso.Rotation * local;
		}

		/// <summary>
		/// Holds the goal inside what the arm can actually reach. Past this the solver straightens the arm
		/// and starts dragging the shoulder after it, which reads as the whole body sagging.
		/// </summary>
		private Vector3 ClampReach(Vector3 goal, float armLength, out float over)
		{
			over = 0f;
			if (shoulder == null || armLength <= 0f)
			{
				return goal;
			}

			float reach = armLength * reachFraction;
			Vector3 offset = goal - shoulder.position;
			float distance = offset.magnitude;
			// 1 is the arm straight: which swing phase is reach-limited rather than weight-limited.
			debugReachRatio = reach > 0f ? distance / reach : 0f;
			if (distance <= reach || distance < 0.0001f)
			{
				return goal;
			}

			over = distance - reach;
			return shoulder.position + offset * (reach / distance);
		}

		/// <summary>Rotation vector taking <paramref name="from"/> to <paramref name="to"/>, in radians.</summary>
		private static Vector3 RotationError(Quaternion from, Quaternion to)
		{
			Quaternion difference = to * Quaternion.Inverse(from);
			if (difference.w < 0f)
			{
				// The long way round is the same rotation; negating takes the short one.
				difference = new Quaternion(-difference.x, -difference.y, -difference.z, -difference.w);
			}

			difference.ToAngleAxis(out float angle, out Vector3 axis);
			if (float.IsInfinity(axis.x) || axis.sqrMagnitude < 0.0000001f || angle < 0.0001f)
			{
				return Vector3.zero;
			}

			return axis.normalized * (angle * Mathf.Deg2Rad);
		}

		/// <summary>Applies a rotation vector, in radians, to a rotation.</summary>
		private static Quaternion Twist(Quaternion rotation, Vector3 rotationVector)
		{
			float angle = rotationVector.magnitude;
			return angle < 0.000001f
				? rotation
				: Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotationVector / angle) * rotation;
		}

		/// <summary>
		/// Strength against the whole limb being carried, weapon mass folded in — the same quantity the
		/// melee performer wields by, read from the same stat, so the two can never disagree.
		/// </summary>
		private float WieldRatio()
		{
			float strength = agent.Stats.TryGetStat(AgentStatIdentifiers.STRENGTH, out EntityStat s) ? s : 1f;
			if (limbMassStat == null)
			{
				return 1f;
			}

			float mass = limbMassStat;
			return mass > 0f ? Mathf.Max(strength / mass, 0f) : 1f;
		}

		/// <summary>
		/// The body's own acceleration in m/s². RigidbodyWrapper averages a velocity delta PER PHYSICS STEP,
		/// so it takes the step duration to become an acceleration comparable with gravity.
		/// </summary>
		private Vector3 BodyAcceleration()
		{
			return agent.Body != null && agent.Body.HasRigidbody && Time.fixedDeltaTime > 0f
				? agent.Body.RigidbodyWrapper.Acceleration / Time.fixedDeltaTime
				: Vector3.zero;
		}

		/// <summary>The body's world velocity, or zero for anything not on a rigidbody.</summary>
		private Vector3 Velocity()
		{
			return agent.Body != null && agent.Body.HasRigidbody
				? agent.Body.RigidbodyWrapper.Velocity
				: Vector3.zero;
		}

		/// <summary>How fast the body is going, all axes — a fall counts.</summary>
		private float Speed()
		{
			return Velocity().magnitude;
		}

		/// <summary>How much harder this armament is to turn than its mass alone implies.</summary>
		private float Leverage(float gyration)
		{
			float ratio = gyration / Mathf.Max(referenceGyration, 0.01f);
			return Mathf.Max(ratio * ratio, 0.0001f);
		}

		/// <summary>
		/// Constrains the elbow without moving the goal: blending rest→rest leaves the authored hint be.
		/// </summary>
		private void ApplyElbowHint(float weight)
		{
			if (ik.TryGetHintRest(ikChain, out Vector3 rest))
			{
				ik.AddHintInfluencer(this, ikChain, ikPrio, rest, weight);
			}
		}

		private void Snap(Vector3 position, Quaternion rotation)
		{
			gripPos = position;
			posVelocity = Vector3.zero;
			gripRot = rotation;
			angularVelocity = Vector3.zero;
		}

		private void OnSheathedEvent(bool sheathed)
		{
			// Reset variables.
			Snap(Vector3.zero, Quaternion.identity);
			settled = false;
			avoidPosed = false;
			avoidNormal = Vector3.zero;
			avoidEscape = Vector3.zero;
			avoidAngle = avoidTarget = 0f;
			ik.RemoveInfluencer(this, ikChain);
			ik.RemoveHintInfluencer(this, ikChain);
		}

		/// <summary>
		/// A world point in the body's own frame. EVERY logged position goes through this: at running speed
		/// the agent travels further per frame than any lag being measured, and world numbers hide all of it.
		/// </summary>
		private Vector3 Local(Vector3 world)
		{
			return agent.Transform.InverseTransformPoint(world);
		}

		/// <summary>A world direction in the body's own frame, for the same reason.</summary>
		private Vector3 Dir(Vector3 world)
		{
			return agent.Transform.InverseTransformDirection(world);
		}

		/// <summary>
		/// Dumps everything the carry model is working from, so a single run explains its behaviour.
		/// </summary>
		private void LogFrame(Vector3 targetPos, Quaternion targetRot, Quaternion slotRot, Vector3 aimLocal,
			ICarryableItem carryable, float wieldRatio, float wield,
			float gyration, float pull, float armLength, float maxLag, float delta)
		{
			string tag = isLeft ? "CARRY-L" : "CARRY-R";

			if (!debugLogged)
			{
				debugLogged = true;
				Vector3 com = carryable == null ? Vector3.zero :
					carryable.Grip.rotation.Inverse() * (carryable.CenterOfMass - carryable.GripPosition);
				SpaxDebug.Log($"[{tag}-INIT]",
					$" item={equipedData.RuntimeItemData.ItemData.Identification?.Name}" +
					$" massItem={equipedData.RuntimeItemData.ItemData.Mass:0.###} limbMass={(limbMassStat == null ? -1f : limbMassStat.Value):0.###}" +
					$" strength={(agent.Stats.TryGetStat(AgentStatIdentifiers.STRENGTH, out EntityStat st) ? st.Value : -1f):0.###}" +
					$" wieldRatio={wieldRatio:0.###} wieldFactor={wield:0.###}" +
					$" | gyration={gyration:0.###} ref={referenceGyration} leverage={Leverage(gyration):0.##}x" +
					$" extent={(carryable != null && carryable.HasExtent ? "authored" : "MEASURED")}" +
					$" aims={(carryable != null && carryable.HasAim)}" +
					$" com={com} armCom={com.magnitude:0.###}" +
					$" | posTime={debugPosTime:0.####}s rotTime={debugRotTime:0.####}s" +
					$" smoothTime={smoothTime} mults={posTimeMult}/{rotTimeMult}" +
					// What each spring settles at under 1g. This is the weight you actually SEE at rest.
					$" | droopRest={pull * debugPosTime * debugPosTime / 4f:0.####}m" +
					$" noseRest={maxSagAngle * Mathf.Clamp01(1f - wield):0.#}deg of {maxSagAngle}" +
					$" | armLen={armLength:0.###} maxLag={maxLag:0.###} maxAngle={maxLagAngle}" +
					$" reach={reachFraction} handR={arms.HandRadius(isLeft):0.###} clearance={clearance}" +
					$" | aim=[{smoothMaxAngle},{absoluteMaxAngle}]x{forwardCorrection} maxPitch={maxPitchAngle} speed=[{aimMinSpeed},{aimMaxSpeed}] | avoid={avoidRays}rays±{avoidSpread} max={maxAvoidAngle} response={avoidResponse}s upBias={upBias} settle={surfaceSmoothTime}s mask={avoidMask.value}{(avoidMask.value == 0 ? " EMPTY-NOTHING-AVOIDED" : "")}" +
					$" | ikPrio={ikPrio} chain={ikChain} grounder={(grounder == null ? "NONE" : "ok")}",
					callerOverride: tag);
			}

			// CLIPPING: only while the blade is actually in something, so a run stays readable.
			// hit is where along the blade it struck, -1 for clear. Applied against asked-for: a gap
			// between them means the response is still catching up.
			if (debugAvoidHit >= 0f || avoidAngle > 0.01f)
			{
				SpaxDebug.Log($"[{tag}-CLIP]",
					$" hit={debugAvoidHit:0.##} n={Dir(debugAvoidNormal)} esc={Dir(avoidEscape)} rise={Mathf.Asin(Mathf.Clamp(avoidEscape.y, -1f, 1f)) * Mathf.Rad2Deg:0.#}" +
					$" | avoid={avoidAngle:0.#}/{avoidTarget:0.#} of {maxAvoidAngle}" +
					// The number that actually decides how bent the pose is. Everything else is one term of it.
					$" bodyTurn={debugBodyTurn:0.#} hang={debugHang:0.###}" +
					$" | blade={Local(avoidGrip)}->{Local(avoidTip)} prio={agent.Priority}",
					callerOverride: tag);
			}

			if (!debugVerbose)
			{
				return;
			}

			// INGREDIENTS, not conclusions. Every direction here is measured off a real transform, so a
			// disagreement between them is a fact rather than something this class decided.
			// swordVsFwd is what the eye actually sees. gripVsSlot tests the one assumption the aim
			// correction rests on: that the item's grip is oriented like the hand slot it was aligned to.
			// If that is not ~0 the aim is being corrected about the wrong axis entirely.
			Vector3 forward = agent.Transform.forward;
			Vector3 sword = carryable != null && carryable.Tip != null
				? (carryable.Tip.position - carryable.GripPosition).normalized
				: Vector3.zero;
			Vector3 aimUsed = targetRot * aimLocal;
			SpaxDebug.Log($"[{tag}-AIM]",
				// sword/aimUsed body-local, so +Z is where the body faces; fwd stays world for bearing.
				$" fwd={forward} sword={Dir(sword)} aimUsed={Dir(aimUsed)}" +
				$" | swordVsFwd={Vector3.Angle(forward, sword):0.#} aimUsedVsFwd={Vector3.Angle(forward, aimUsed):0.#}" +
				$" swordVsAimUsed={Vector3.Angle(sword, aimUsed):0.#}" +
				$" | gripVsSlot={(carryable == null ? -1f : Quaternion.Angle(carryable.Grip.rotation, slotRot)):0.#}" +
				$" | speed={Velocity().FlattenY().magnitude:0.##} ramp={debugAimRamp:0.##}" +
				$" aimFix={debugAimFix:0.#} pitchFix={debugAimPitch:0.#}" +
				$" | lagAngle={debugRotLag:0.#} of {maxLagAngle}" +
				$" avoid={avoidAngle:0.#} bodyTurn={debugBodyTurn:0.#}",
				callerOverride: tag);

			float posLag = debugLag.magnitude;
			float rotLag = debugRotLag;
			float speed = Velocity().FlattenY().magnitude;

			SpaxDebug.Log($"[{tag}]",
				$" dt={delta:0.0000} ts={timescale.Value:0.##} w={arms.Weight.Value:0.###} speed={speed:0.##}" +
				// lag is the whole trail; droop is how much of it is straight down, which is the weight.
				$" | lag={posLag:0.0000} droop={debugDroop:0.0000} rotLag={rotLag:0.##}" +
				// Each half against its OWN limit, with what its own leash kept. 1 is sitting on the limit.
				$" | ext pos={debugPosExt:0.##}x{debugScale:0.###} rot={debugRotExt:0.##}x{debugRotScale:0.###}" +
				$" | aim={debugAim:0.#} ramp={debugAimRamp:0.##} fix={debugAimFix:0.#} pitch={debugAimPitch:0.#}" +
				// Every guard that moved the goal after the model was done with it.
				$" | hang={debugHang:0.###} accel={debugAccel:0.##} bodyTurn={debugBodyTurn:0.#} avoid={avoidAngle:0.#}/{avoidTarget:0.#} hit={debugAvoidHit:0.##} n={Dir(debugAvoidNormal)} esc={Dir(avoidEscape)} prio={agent.Priority} push={debugPushed:0.###} reach={debugReachRatio:0.##} over={debugReached:0.###}" +
				$" | snap={(debugSnapped ? 1 : 0)} tp={(debugTeleported ? 1 : 0)}" +
				// BODY-LOCAL, all of them. In world space the agent's own travel swamps every lag here.
				$" | lag={debugLag} grip={gripPos} tgt={debugTargetLocal} goal={Local(debugHandGoal)} hand={Local(hand.position)}",
				callerOverride: tag);

			debugFrames++;
			debugSumLag += posLag;
			debugPeakLag = Mathf.Max(debugPeakLag, posLag);
			debugSumRotLag += rotLag;
			debugPeakRotLag = Mathf.Max(debugPeakRotLag, rotLag);
			debugSumDroop += debugDroop;
			debugPeakDroop = Mathf.Max(debugPeakDroop, debugDroop);
			debugSumExt += debugPosExt;
			debugPeakExt = Mathf.Max(debugPeakExt, debugPosExt);
			debugSumSpeed += speed;
			debugPeakAvoid = Mathf.Max(debugPeakAvoid, avoidAngle);
			if (debugSnapped) { debugSnaps++; }
			if (avoidAngle > 0.01f) { debugLifts++; }
			if (debugPushed > 0.0001f) { debugPushes++; }
			if (debugReached > 0.0001f) { debugReaches++; }

			debugTimer += delta;
			if (debugTimer < 1f || debugFrames == 0)
			{
				return;
			}

			SpaxDebug.Log($"[{tag}-SUM]",
				$" {debugFrames}f speed avg={debugSumSpeed / debugFrames:0.##}" +
				$" | lag avg={debugSumLag / debugFrames:0.0000} peak={debugPeakLag:0.0000} of {maxLag:0.###}" +
				$" | droop avg={debugSumDroop / debugFrames:0.0000} peak={debugPeakDroop:0.0000}" +
				$" | rotLag avg={debugSumRotLag / debugFrames:0.##} peak={debugPeakRotLag:0.##} of {maxLagAngle}" +
				$" | posExt avg={debugSumExt / debugFrames:0.###} peak={debugPeakExt:0.###}" +
				// Anything but 0 here means a guard is doing work the model should have done.
				$" | peakAvoid={debugPeakAvoid:0.#} avoids={debugLifts} pushes={debugPushes} reaches={debugReaches}" +
				$" snaps={debugSnaps}",
				callerOverride: tag);

			debugTimer = 0f;
			debugFrames = 0;
			debugSumLag = debugPeakLag = debugSumRotLag = debugPeakRotLag = 0f;
			debugSumDroop = debugPeakDroop = debugSumExt = debugPeakExt = debugSumSpeed = 0f;
			debugPeakAvoid = 0f;
			debugSnaps = debugLifts = debugPushes = debugReaches = 0;
		}

		private void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			if (hand == null)
			{
				hand = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			}

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hand.position, 0.02f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(debugTarget, debugMaxLag);
			Gizmos.color = Color.red;
			Gizmos.DrawLine(debugTarget, debugGripWorld);
			Gizmos.DrawSphere(debugGripWorld, 0.02f);
			// Where the model put the armament's line, and the goal the arm was actually handed.
			Gizmos.color = Color.cyan;
			Gizmos.DrawLine(debugGripWorld, debugTip);
			Gizmos.DrawSphere(debugHandGoal, 0.015f);

			// The blade the cast measured, and which way it decided to steer out.
			if (avoidTarget > 0.01f)
			{
				Gizmos.color = Color.green;
				Gizmos.DrawLine(avoidGrip, avoidTip);
				Gizmos.DrawRay(avoidTip, avoidEscape * 0.25f);
			}
		}
	}
}
