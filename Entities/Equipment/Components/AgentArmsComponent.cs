using System;
using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component which supplies the arms' equipment slots and owns what each arm is doing:
	/// which armament is active, whether it is drawn, and the transition between.
	/// </summary>
	public class AgentArmsComponent : AgentComponentBase, IPerformer
	{
		/// <summary>Seconds a leg spends taking the hand off the animation, or handing it back.</summary>
		private const float HANDOVER_TIME = 0.1f;

		/// <summary>Degrees per second the elbow may travel round its ring. THE knob for elbow jitter.</summary>
		private const float ELBOW_TURN_RATE = 160f;

		/// <summary>How far over the shoulder, in arm lengths, a grip must sit to be reached over it.</summary>
		private const float OVER_SHOULDER_MARGIN = 0.1f;

		/// <summary>Share of the arm a slide may use. Below 1 it stops short of a locked-out arm.</summary>
		private const float SLIDE_REACH = 0.8f;

		/// <summary>How finely a swap's route is drawn. Enough that the arc reads as a curve.</summary>
		private const int PATH_GIZMO_SAMPLES = 48;

		/// <summary>Where a reach behind the shoulder passes, above and in front of it, in arm lengths.</summary>
		private const float OVER_SHOULDER_RISE = 0.35f;
		private const float OVER_SHOULDER_LEAD = 0.4f;

		/// <summary>How the elbow's ring is searched for the body's edge: coarse walk, then bisection onto it.</summary>
		private const float CLEAR_STEP = 2f;
		private const int CLEAR_BISECTIONS = 6;

		/// <summary>The bones whose own colliders the arc is kept clear of. Limbs and head are not in its way.</summary>
		private static readonly string[] TORSO_BONES =
		{
			HumanBoneIdentifiers.HIPS, HumanBoneIdentifiers.SPINE,
			HumanBoneIdentifiers.CHEST, HumanBoneIdentifiers.UPPER_CHEST
		};

		/// <summary>
		/// Invoked when the sheathed state is changed.
		/// </summary>
		public event Action<bool> SheathedEvent;

		/// <summary>
		/// Invoked whenever an arm's contents change — equipped, unequipped, wielded or stowed.
		/// Carries the whole arm so listeners never have to query back.
		/// </summary>
		public event Action<ArmState> ArmChangedEvent;

		#region IPerformer events

		public event Action<IPerformer> StartedPreparingEvent;
		public event Action<IPerformer> StartedPerformingEvent;
		public event Action<IPerformer> PerformanceUpdateEvent;
		public event Action<IPerformer> PerformanceCompletedEvent;

		#endregion

		/// <summary>
		/// Composite weight float for both arms that can be influenced by external modifiers.
		/// Is read by IK system controlling arms, this class does not use it itself!
		/// </summary>
		public CompositeFloat Weight { get; set; } = new CompositeFloat(1f);

		public Transform LeftHand => lookup.Lookup(HumanBoneIdentifiers.LEFT_HAND);
		public Transform RightHand => lookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND);
		public Transform LeftSheathe => lookup.Lookup(TransformLookupIdentifiers.LEFT_SHEATHE);
		public Transform RightSheathe => lookup.Lookup(TransformLookupIdentifiers.RIGHT_SHEATHE);

		/// <summary>
		/// The ACTUAL sheathed state. <see cref="SetSheathed"/> sets the desired state, which this follows.
		/// </summary>
		public bool Sheathed { get; private set; } = true;

		public ArmState Left => leftArm;
		public ArmState Right => rightArm;

		/// <summary>The armament currently in the left hand, if any.</summary>
		public RuntimeEquipedData LeftEquip => leftArm.Wielded;
		/// <summary>The armament currently in the right hand, if any.</summary>
		public RuntimeEquipedData RightEquip => rightArm.Wielded;
		public GameObject LeftVisual => LeftEquip == null ? null : LeftEquip.EquipedInstance;
		public GameObject RightVisual => RightEquip == null ? null : RightEquip.EquipedInstance;

		#region IPerformer properties

		/// <inheritdoc/>
		public int Priority => 0;
		/// <inheritdoc/>
		public IAct Act { get; private set; }
		/// <inheritdoc/>
		public PerformanceState State { get; private set; } = PerformanceState.Inactive;
		/// <inheritdoc/>
		public float RunTime { get; private set; }
		/// <inheritdoc/>
		public bool Paused { get; set; }
		/// <inheritdoc/>
		public bool Canceled { get; private set; }
		/// <inheritdoc/>
		public float CancelTime { get; private set; }

		// Explicit: this class already exposes a CompositeFloat named Weight for the IK system.
		// Arms transitions never pose the body, so the performance weight is always 0.
		float IPerformer.Weight => 0f;

		#endregion

		protected TransformLookup SafeLookup
		{
			get
			{
				if (lookup == null)
				{
					lookup = gameObject.GetComponentRelative<TransformLookup>();
				}
				if (lookup == null)
				{
					lookup = gameObject.AddComponent<TransformLookup>();
				}
				return lookup;
			}
		}

		/// <summary>The body's collider registry. Found without injection so gizmos work in edit mode too.</summary>
		protected IAgentBody SafeBody
		{
			get
			{
				if (agentBody == null)
				{
					agentBody = gameObject.GetComponentRelative<IAgentBody>();
				}
				return agentBody;
			}
		}

		[SerializeField, HideInInspector] private bool left;
		[SerializeField, Conditional(nameof(left), drawToggle: true), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string leftType;
		[SerializeField, HideInInspector] private bool right;
		[SerializeField, Conditional(nameof(right), drawToggle: true), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string rightType;
		[SerializeField, Min(1), Tooltip("How many armaments each arm can carry. Only one is in hand at a time.")]
		private int slotsPerArm = 2;
		[SerializeField] private bool drawGizmos;

		[Header("Transition")]
		[SerializeField] private int ikPriority = 2;
		[SerializeField, Min(0.01f), Tooltip("Hand travel speed in m/s. Every leg's duration follows the distance it actually covers.")]
		private float handSpeed = 1.5f;
		[SerializeField, Min(0f), Tooltip("Floor on a leg's duration, so a swap within one slot still reads as a motion.")]
		private float minLegDuration = 0.08f;
		[SerializeField, Range(0f, 1f), Tooltip("Movement control retained while transitioning. 1 = full control (sheathing while moving).")]
		private float transitionControl = 1f;
		[SerializeField, Min(0f), Tooltip("Seconds a swap must be held before it unarms the arm instead of cycling.")]
		private float unarmThreshold = 0.3f;

		[Header("Reach path")]
		[SerializeField, Range(0f, 1f), Tooltip("How much of the full half circle the swing rides. Below 1 where a full one carries the hand out of reach.")]
		private float arcBulge = 1f;
		[SerializeField, Tooltip("How the grip correction spreads across the swing. The arm's own carry is always linear.")]
		private EasingMethod gripEasing = EasingMethod.InOutSine;

		[Header("DEBUGGING")]
		[SerializeField, Tooltip("Log how each leg's path is shaped, as it begins.")]
		private bool debugPath;
		[SerializeField] private GameObject testPrefab;
		[SerializeField] private bool testLeft;
		[SerializeField] private bool testRight;

		/// <summary>Reach cones found on the upper arms, looked up once. A null entry means none authored.</summary>
		private readonly Dictionary<Transform, RotationLimit> jointLimits = new Dictionary<Transform, RotationLimit>();

		private TransformLookup lookup;
		private EquipmentComponent equipment;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;
		private AgentSheatheComponent sheathe;
		private IAgentBody agentBody;
		private EntityStat entityTimeScale;

		/// <summary>The torso's own capsules, refilled each time a path is built rather than reallocated.</summary>
		private readonly List<ArmPath.BodyCapsule> bodyCapsules = new List<ArmPath.BodyCapsule>();
		private ArmPath.BodyCapsule[] bodyCapsuleBuffer;

		private ArmState leftArm;
		private ArmState rightArm;

		private bool desiredSheathed = true;

		/// <summary>Whether this performer is registered with the Actor right now.</summary>
		private bool active;

		private readonly List<PendingSwap> pendingSwaps = new List<PendingSwap>();
		private readonly List<ArmTransition> transitions = new List<ArmTransition>();
		private FloatOperationModifier controlMod;

		/// <summary>Last frame's animated hand poses, in torso space. See <see cref="SampleAnimatedHands"/>.</summary>
		private (Vector3 pos, Quaternion rot) leftAnimated;
		private (Vector3 pos, Quaternion rot) rightAnimated;

		/// <summary>The animated arms' own shoulder-to-hand line and elbow direction off it, in agent space.</summary>
		private (Vector3 axis, Vector3 roll) leftSwivel;
		private (Vector3 axis, Vector3 roll) rightSwivel;
		private bool sampledAnimated;

		public void InjectDependencies(IEntity entity, EquipmentComponent equipment, TransformLookup lookup,
			CallbackService callbackService, [Optional] IIKComponent ik, [Optional] RigidbodyWrapper rigidbodyWrapper,
			[Optional] AgentSheatheComponent sheathe, [Optional] IAgentBody agentBody)
		{
			this.equipment = equipment;
			this.lookup = lookup;
			this.callbackService = callbackService;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.sheathe = sheathe;
			this.agentBody = agentBody;

			entityTimeScale = entity.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, false);
		}

		#region Editor
#if UNITY_EDITOR
		protected void OnValidate()
		{
			if (testLeft)
			{
				InstantiateTest(true);
				testLeft = false;
			}

			if (testRight)
			{
				InstantiateTest(false);
				testRight = false;
			}
		}

		private void InstantiateTest(bool isLeft)
		{
			GameObject instance = Instantiate(testPrefab, isLeft ?
				SafeLookup.Lookup(HumanBoneIdentifiers.LEFT_HAND) :
				SafeLookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND));
			instance.transform.localScale = Vector3.one.Divide(instance.transform.lossyScale);
			WeaponComponent weapon = instance.GetComponentInChildren<WeaponComponent>();
			(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(isLeft, false,
				weapon == null ? AgentSheatheComponent.DEFAULT_WIELD_RADIUS : weapon.WieldRadius);
			AlignGrip(instance.transform, weapon == null ? null : weapon.MainHand, orientation.pos, orientation.rot);
		}
#endif
		#endregion

		protected void Awake()
		{
			leftArm = new ArmState(true);
			rightArm = new ArmState(false);

			if (left) { CreateSlots(leftArm, leftType); }
			if (right) { CreateSlots(rightArm, rightType); }

			void CreateSlots(ArmState arm, string type)
			{
				for (int i = 0; i < slotsPerArm; i++)
				{
					int index = i;
					EquipmentSlot slot = new EquipmentSlot(
						SlotID(arm.IsLeft, index), type,
						(data) => OnEquip(arm, index, data),
						(data) => OnUnequip(arm, index, data));

					equipment.AddSlot(slot);
					arm.AddSlot(slot);
				}
			}
		}

		protected void OnEnable()
		{
			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, SampleAnimatedHands);
		}

		protected void OnDisable()
		{
			callbackService.UnsubscribeUpdates(this);

			if (active)
			{
				// Land the armaments wherever the transition was taking them, and release the Actor.
				foreach (ArmTransition transition in transitions)
				{
					ApplyArm(transition.Arm);
				}
				UpdateSheathedFlag();
				State = PerformanceState.Completed;
				PerformanceCompletedEvent?.Invoke(this);
			}

			EndTransition();
		}

		protected void OnDestroy()
		{
			RemoveSlots(leftArm);
			RemoveSlots(rightArm);

			void RemoveSlots(ArmState arm)
			{
				if (arm == null)
				{
					return;
				}
				for (int i = 0; i < arm.Slots.Count; i++)
				{
					equipment.RemoveSlot(arm.Slots[i].ID);
				}
			}
		}

		/// <summary>The ID of the <paramref name="index"/>th slot on an arm.</summary>
		private static string SlotID(bool isLeft, int index)
		{
			return $"{(isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND)}_{index}";
		}

		public ArmState GetArm(bool isLeft)
		{
			return isLeft ? leftArm : rightArm;
		}

		#region Requests

		/// <summary>
		/// Requests the arms be sheathed or unsheathed. Idempotent, and latches until satisfied —
		/// the request survives being unable to act on it right now.
		/// </summary>
		public void SetSheathed(bool sheathed)
		{
			desiredSheathed = sheathed;
		}

		/// <summary>
		/// Makes slot <paramref name="index"/> the arm's active armament. -1 unarms it.
		/// The last such request always wins, including over a two-handed grip on the other arm.
		/// </summary>
		public void SetActiveSlot(ArmState arm, int index)
		{
			if (arm == null || arm.Slots.Count == 0 || index >= arm.Slots.Count)
			{
				return;
			}
			if (index >= 0 && arm.Armaments[index] == null)
			{
				return;
			}

			arm.ActiveIndex = index;
			if (index >= 0)
			{
				arm.LastActiveIndex = index;

				// Two hands can't both be full when either grip needs both of them.
				ArmState other = arm.IsLeft ? rightArm : leftArm;
				if (other.ActiveIndex >= 0 && (IsTwoHanded(arm.Active) || IsTwoHanded(other.Active)))
				{
					other.ActiveIndex = -1;
					RefreshStowOrder(other);
					ArmChangedEvent?.Invoke(other);
				}
			}

			// The stack shows which armament is next out, so it re-sorts even when nothing is drawn.
			RefreshStowOrder(arm);
			ArmChangedEvent?.Invoke(arm);
		}

		/// <summary>
		/// Moves to the next occupied slot on this arm, skipping empties.
		/// From unarmed, returns to whatever was last held.
		/// </summary>
		public void CycleArm(ArmState arm)
		{
			int count = arm.Slots.Count;
			if (count == 0)
			{
				return;
			}

			if (arm.ActiveIndex < 0 &&
				arm.LastActiveIndex >= 0 && arm.LastActiveIndex < count &&
				arm.Armaments[arm.LastActiveIndex] != null)
			{
				SetActiveSlot(arm, arm.LastActiveIndex);
				return;
			}

			for (int step = 1; step <= count; step++)
			{
				int index = (((arm.ActiveIndex + step) % count) + count) % count;
				if (arm.Armaments[index] != null)
				{
					SetActiveSlot(arm, index);
					return;
				}
			}
		}

		/// <summary>Stows whatever this arm is holding without giving up its armaments.</summary>
		public void UnarmArm(ArmState arm)
		{
			SetActiveSlot(arm, -1);
		}

		#endregion Requests

		#region Transition

		/// <summary>What this arm should have in hand right now.</summary>
		private RuntimeEquipedData Intended(ArmState arm)
		{
			return desiredSheathed ? null : arm.Active;
		}

		/// <summary>Whether this arm is holding something other than what it should be.</summary>
		private bool Diverges(ArmState arm)
		{
			return Intended(arm) != arm.Wielded;
		}

		private void OnUpdate(float delta)
		{
			float scaledDelta = delta * (entityTimeScale ?? 1f);

			if (!active)
			{
				Reconcile();
				return;
			}

			if (!Paused)
			{
				RunTime += scaledDelta;
			}

			AdvancePendingSwaps(scaledDelta);
			RunLegs(scaledDelta);
			UpdateControl();
			UpdateState();

			PerformanceUpdateEvent?.Invoke(this);

			if (State == PerformanceState.Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
				EndTransition();
				return;
			}

			// An arm freed up mid-performance can still be claimed by a pending sheathe request.
			if (State == PerformanceState.Finishing)
			{
				Reconcile();
			}
		}

		/// <summary>
		/// Preparing while a swap is still deciding, Finishing once every arm is only travelling home.
		/// </summary>
		private void UpdateState()
		{
			if (pendingSwaps.Count == 0 && transitions.Count == 0)
			{
				State = PerformanceState.Completed;
				return;
			}

			if (pendingSwaps.Count > 0)
			{
				State = PerformanceState.Preparing;
				return;
			}

			foreach (ArmTransition transition in transitions)
			{
				if (transition.Leg != TransitionLeg.Recover)
				{
					State = PerformanceState.Performing;
					return;
				}
			}

			State = PerformanceState.Finishing;
		}

		/// <summary>Counts each held swap toward the unarm threshold.</summary>
		private void AdvancePendingSwaps(float delta)
		{
			for (int i = pendingSwaps.Count - 1; i >= 0; i--)
			{
				PendingSwap pending = pendingSwaps[i];
				if (!Paused)
				{
					pending.Time += delta;
				}

				// Held long enough to mean "put it away" rather than "next one".
				if (pending.Time >= unarmThreshold)
				{
					CommitSwap(pending, true);
				}
			}
		}

		/// <summary>
		/// Drives actual state toward desired. Runs every frame the arms are idle, so a request that
		/// couldn't be honoured earlier is honoured as soon as it can be.
		/// </summary>
		private void Reconcile()
		{
			// Only bother when an arm that diverges is actually free to be claimed.
			if (!(Diverges(leftArm) && Claimable(leftArm)) && !(Diverges(rightArm) && Claimable(rightArm)))
			{
				return;
			}

			IAgent agent = Agent;
			IActor actor = agent == null ? null : agent.Actor;

			if (ik == null || actor == null)
			{
				// Nothing to animate with — flip straight over.
				ApplyArm(leftArm);
				ApplyArm(rightArm);
				UpdateSheathedFlag();
				return;
			}

			// Wait for any running performance rather than interrupting it.
			if (actor.MainPerformer != null &&
				actor.State != PerformanceState.Finishing && actor.State != PerformanceState.Completed)
			{
				return;
			}

			actor.Send(new ActSignal(ActorActs.SHEATHE, interuptable: true, interuptor: false));
		}

		/// <summary>
		/// Whether a new act may take this arm. An arm already travelling out is off limits, but one that
		/// has handed over and is only recovering can be claimed again — that is what lets swaps chain.
		/// </summary>
		private bool Claimable(ArmState arm)
		{
			foreach (PendingSwap pending in pendingSwaps)
			{
				if (pending.Arm == arm)
				{
					return false;
				}
			}

			foreach (ArmTransition transition in transitions)
			{
				if (transition.Arm == arm && transition.Leg != TransitionLeg.Recover)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Takes an arm back off a transition that is only recovering.</summary>
		private void ClaimArm(ArmState arm)
		{
			for (int i = transitions.Count - 1; i >= 0; i--)
			{
				if (transitions[i].Arm == arm)
				{
					transitions.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Starts one arm's leg sequence, or applies the change outright when there is nothing to animate.
		/// </summary>
		private void StartTransition(ArmState arm)
		{
			RuntimeEquipedData target = Intended(arm);
			if (target == arm.Wielded)
			{
				return;
			}

			if (ik == null)
			{
				ApplyArm(arm);
				UpdateSheathedFlag();
				return;
			}

			ClaimArm(arm);

			ArmTransition transition = new ArmTransition
			{
				Arm = arm,
				Stowing = arm.Wielded,
				Drawing = target
			};
			transitions.Add(transition);

			if (debugPath)
			{
				SpaxDebug.Log("ARMSWAP", $"{(arm.IsLeft ? "LEFT" : "RIGHT")} begins" +
					$" | stowing {Name(transition.Stowing)} | drawing {Name(transition.Drawing)}" +
					$" | holds {arm.Armaments.Count} of {slotsPerArm}");
			}

			// Where the arm was before we took it — recovery brings it back here.
			CaptureHome(transition);

			// How Drawing comes clear of ITS sheathe, while it is still sitting there to measure.
			(transition.WithdrawAxis, transition.WithdrawDepth) = InsertMotion(arm, transition.Drawing);

			// Reserve the resting place up front, so the hand has somewhere definite to reach for.
			if (sheathe != null && transition.Stowing != null)
			{
				sheathe.TryAssign(transition.Stowing, arm.Side, StowOrder(arm, transition.Stowing));
			}

			BeginLeg(transition, transition.Stowing != null ? TransitionLeg.ToStow : TransitionLeg.ToDraw);
		}

		/// <summary>
		/// Advances every arm's leg. Each leg's length is the distance the hand actually covers, so a
		/// swap between two armaments sharing a resting place costs almost nothing.
		/// </summary>
		private void RunLegs(float delta)
		{
			for (int i = transitions.Count - 1; i >= 0; i--)
			{
				ArmTransition transition = transitions[i];
				AdvanceLeg(transition, delta);

				if (transition.Leg == TransitionLeg.Done)
				{
					transitions.RemoveAt(i);
				}
			}
		}

		private void AdvanceLeg(ArmTransition transition, float delta)
		{
			if (!Paused)
			{
				transition.Time += delta;
			}

			float progress = transition.Duration <= 0f ? 1f : Mathf.Clamp01(transition.Time / transition.Duration);
			float eased = progress.InOutCubic();

			ArmPath path = BuildPath(transition);
			(Vector3 pos, Quaternion rot, float arc) pose = path.Evaluate(eased);
			float authority = Authority(transition, pose.pos);
			float weight = LegWeight(transition, progress, authority);
			transition.Weight = weight;
			transition.Commitment = LegCommitment(transition, eased);

			// The path decides the hand, and the elbow is only a bend preference again. It used to be the
			// other way about — the hand read off the elbow, and the elbow was fought over by five rules
			// that knew nothing of the grip, so the hand inherited every one of their arguments.
			Quaternion hand = pose.rot;
			ApplyElbowHint(transition, pose.pos, weight, authority, delta);

			ik.AddInfluencer(this, transition.Arm.IKChain, ikPriority, pose.pos, weight, hand, weight);

			if (debugPath)
			{
				Transform handBone = transition.Arm.IsLeft ? LeftHand : RightHand;
				Quaternion twist = Quaternion.Inverse(Agent.Transform.rotation) * BodyFrame().rotation;

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
		}

		/// <summary>
		/// The hand's route for this leg, rebuilt from live endpoints so it follows a body that is moving.
		/// Only the time splits are held fixed, from when the leg began.
		/// </summary>
		private ArmPath BuildPath(ArmTransition transition)
		{
			Transform agentTransform = Agent.Transform;
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
				CarriedIn = transition.CarriedIn,
				TurnsOver = transition.TurnsOver,
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
		private ArmPath.BodyCapsule[] BodyCapsules()
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Quaternion inverse = Quaternion.Inverse(body.rotation);

			bodyCapsules.Clear();
			for (int i = 0; i < TORSO_BONES.Length; i++)
			{
				if (SafeBody == null || !SafeBody.TryGetBoneColliders(TORSO_BONES[i], out IReadOnlyList<Collider> colliders))
				{
					continue;
				}

				for (int c = 0; c < colliders.Count; c++)
				{
					if (colliders[c] is CapsuleCollider capsule && Capsule(capsule, inverse, body.origin,
						out ArmPath.BodyCapsule shape))
					{
						bodyCapsules.Add(shape);
					}
				}
			}

			if (bodyCapsuleBuffer == null || bodyCapsuleBuffer.Length != bodyCapsules.Count)
			{
				bodyCapsuleBuffer = new ArmPath.BodyCapsule[bodyCapsules.Count];
			}
			bodyCapsules.CopyTo(bodyCapsuleBuffer);
			return bodyCapsuleBuffer;
		}

		/// <summary>
		/// One collider reduced to its two end-sphere centres and a radius, in the body's frame. Scale is
		/// read the way Unity reads it: radius off the two axes across, length off the one along.
		/// </summary>
		private static bool Capsule(CapsuleCollider capsule, Quaternion inverse, Vector3 origin,
			out ArmPath.BodyCapsule shape)
		{
			shape = default;
			Transform owner = capsule.transform;
			Vector3 scale = owner.lossyScale;
			Vector3 absolute = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

			Vector3 axis;
			float radiusScale;
			float lengthScale;
			switch (capsule.direction)
			{
				case 2: axis = Vector3.forward; radiusScale = Mathf.Max(absolute.x, absolute.y); lengthScale = absolute.z; break;
				case 1: axis = Vector3.up; radiusScale = Mathf.Max(absolute.x, absolute.z); lengthScale = absolute.y; break;
				default: axis = Vector3.right; radiusScale = Mathf.Max(absolute.y, absolute.z); lengthScale = absolute.x; break;
			}

			float radius = capsule.radius * radiusScale;
			if (radius <= 0f)
			{
				return false;
			}

			// Unity clamps a capsule's height to its own diameter, so the segment can be nothing at all.
			float half = Mathf.Max(0f, Mathf.Max(capsule.height * lengthScale, radius * 2f) * 0.5f - radius);
			Vector3 centre = owner.TransformPoint(capsule.center);
			Vector3 along = owner.rotation * axis * half;

			shape.Start = inverse * (centre - along - origin);
			shape.End = inverse * (centre + along - origin);
			shape.Radius = radius;
			return true;
		}

		/// <summary>
		/// The pelvis' frame — the thing the arm is reaching around, and the one part of the torso the
		/// arm cannot move: full-body IK lets a reach drag the shoulders with it, so a frame taken from
		/// them would be partly an output of the reach it is meant to decide.
		/// Built from bone positions alone, all three rigid to the pelvis, so no rig's axis convention
		/// or animated twist can enter into it.
		/// </summary>
		private (Vector3 origin, Quaternion rotation) BodyFrame()
		{
			Transform agentTransform = Agent.Transform;
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

		/// <summary>
		/// How much of the hand this leg owns. The arc is only meaningful at full weight, so a leg taking
		/// over from animation ramps in quickly; one handing back follows the hand home instead, and so is
		/// spent by the time it arrives. The clock only bounds it, for a hand that never quite gets there.
		/// </summary>
		private static float LegWeight(ArmTransition transition, float t, float authority)
		{
			// A fixed handover however long the leg is — it takes what it takes to not pop.
			float blend = transition.Duration <= 0f ? 1f : Mathf.Clamp01(HANDOVER_TIME / transition.Duration);

			switch (transition.Blend)
			{
				case LegBlend.In:
					return Mathf.Clamp01(t / blend);
				case LegBlend.Out:
					return Mathf.Min(authority, Mathf.Clamp01((1f - t) / blend));
				default:
					return 1f;
			}
		}

		/// <summary>
		/// How far out on a limb this leg has the arm, for movement control. Separate from IK weight, which
		/// now commits almost immediately and would otherwise snap control down with it.
		/// </summary>
		private static float LegCommitment(ArmTransition transition, float t)
		{
			switch (transition.Blend)
			{
				case LegBlend.In:
					return t;
				case LegBlend.Out:
					return 1f - t;
				default:
					return 1f;
			}
		}

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

			if (shoulder == null || elbow == null ||
				!LimbHint.TryGetCircle(shoulder, elbow, handBone, hand,
					out Vector3 centre, out float radius, out Vector3 axis))
			{
				return;
			}

			// The grip does NOT pull the elbow. Nothing downstream reads the elbow for the hand's rotation
			// any more, so it is free to be only what it should be: where the arm prefers to bend.
			float folded = 1f - Mathf.Clamp01(Vector3.Distance(shoulder.position, hand)
				/ Mathf.Max(ArmLength(transition.Arm.IsLeft), 0.0001f));

			// The ANIMATION's elbow, turned by however far this arm has turned away from the pose it was
			// read in. Carried a frame at a time so every step is between two near-identical lines, and
			// only ever turned — nothing downstream is allowed back into it, so it cannot drift.
			Quaternion agent = Agent.Transform.rotation;
			Vector3 armLine = Quaternion.Inverse(agent) * axis;
			(Vector3 axis, Vector3 roll) animated = transition.Arm.IsLeft ? leftSwivel : rightSwivel;

			if (!transition.HasSwivel && sampledAnimated && animated.axis != Vector3.zero)
			{
				transition.SwivelAxis = animated.axis;
				transition.SwivelSeed = animated.roll;
				transition.HasSwivel = true;
			}

			// ONE turn from the pinned original onto the line the arm has now. Composing a step per frame
			// instead is parallel transport, and it accumulates the swept solid angle as a false twist.
			if (transition.HasSwivel)
			{
				transition.SwivelRoll = Quaternion.FromToRotation(transition.SwivelAxis, armLine) *
					transition.SwivelSeed;
			}

			Vector3 neutral = Vector3.ProjectOnPlane(transition.HasSwivel
				? agent * transition.SwivelRoll
				: -(BodyFrame().rotation * Vector3.up), axis);
			if (neutral.sqrMagnitude < 0.000001f)
			{
				return;
			}
			neutral.Normalize();

			// The elbow IS the animation's own, carried onto the line the arm now has. Nothing else wants
			// a say in it before the body and the joint limit get theirs.
			Vector3 preferred = neutral;

			// Last frame's output, which the limit uses as a place it is known to have been allowed, and
			// which this frame eases off. It never feeds the neutral, so neither a clamp nor the damping
			// can write itself into what the elbow wants next.
			Vector3 was = transition.HasElbowRoll
				? Vector3.ProjectOnPlane(agent * transition.ElbowRoll, axis) : Vector3.zero;
			Vector3 reference = was.sqrMagnitude > 0.000001f ? was.normalized : preferred;

			// An elbow can only turn so fast. Bounding the TARGET rather than the result keeps the body and
			// the joint limit the last authority — both travel from the elbow and stop, so a short target
			// arc is a short travel. A fraction of the gap instead lurches whenever the gap is large.
			float turn = Vector3.SignedAngle(reference, preferred, axis);
			float allowed = ELBOW_TURN_RATE * delta;
			if (Mathf.Abs(turn) > allowed)
			{
				preferred = Quaternion.AngleAxis(Mathf.Sign(turn) * allowed, axis) * reference;
			}

			// Out of the torso before the joint limit gets its say, so the limit stays the last authority on
			// what the arm can actually do. Travelled from where the elbow was, like the limit's own clamp.
			preferred = ClearOfBody(centre, radius, axis, reference, preferred,
				transition.Clearance, out float bodied);

			Vector3 held = HoldWithinLimit(transition.Arm, shoulder, reference, preferred,
				centre, radius, axis, out float clamped, out string mode);
			transition.ElbowRoll = Quaternion.Inverse(agent) * held;
			transition.HasElbowRoll = true;
			Vector3 elbowPoint = centre + held * radius;

			ik.AddHintInfluencer(this, transition.Arm.IKChain, ikPriority, elbowPoint, weight);

			if (debugPath)
			{
				(Vector3 origin, Quaternion rotation) body = BodyFrame();
				Quaternion inverse = Quaternion.Inverse(body.rotation);
				float progress = transition.Duration <= 0f ? 1f : transition.Time / transition.Duration;
				Vector3 outward = body.rotation * (transition.Arm.IsLeft ? Vector3.left : Vector3.right);

				// How far the elbow ACTUALLY travelled this frame, against every term that could move it.
				// A step no term accounts for is the bug; a sign change in aim is a branch cut.
				float travelled = Vector3.Angle(reference, held);

				SpaxDebug.Log("ARMELBOW",
					$"{(transition.Arm.IsLeft ? "LEFT" : "RIGHT")} {transition.Leg} t {progress:0.##}" +
					$" | w {weight:0.###} auth {authority:0.###} folded {folded:0.##}" +
					// drift is how far the elbow ended up from what the animation's swivel asked for. It
					// walking away and parking there is the swivel accumulating, not the limit refusing.
					$" | TRAVELLED {travelled:0.#} drift {Vector3.Angle(neutral, held):0.#}" +
					$" | neutral {inverse * neutral}" +
					// free = limit idle, edge = riding it, RECOVER = it teleported us back inside.
					$" | limit {mode} clamped {clamped:0.#}" +
					// How far into the body the elbow still is once the limit has had its say. Above zero
					// with a large clamp means the joint cone is overruling the body and needs widening.
					// inside = how far into the body the elbow ends up; bodied = how far the body test moved
					// it off what the fingers wanted. A big clamp against a small bodied means they fought.
					$" | inside {BodyDepth(centre + held * radius):0.###}" +
					$" bodied {bodied:0.#}" +
					$" | sideways {Vector3.Angle(centre + held * radius - shoulder.position, outward):0.#}" +
					// 1 means the arm has run out of bend and the elbow circle has collapsed to a point.
					$" | straight {Vector3.Distance(shoulder.position, hand) / Mathf.Max(ArmLength(transition.Arm.IsLeft), 0.0001f):0.###}" +
					$" radius {radius:0.###}" +
					$" | want {inverse * (hand - body.origin)}" +
					$" | hint {inverse * (centre + held * radius - body.origin)}" +
					$" | elbow {inverse * (elbow.position - body.origin)}" +
					$" | shoulder {inverse * (shoulder.position - body.origin)}" +
					// The two directions on the ring: where the elbow was, and where it ended up.
					$" | was {inverse * reference} held {inverse * held}" +
					$" | agrees {Vector3.Dot(Vector3.ProjectOnPlane(elbow.position - centre, axis).normalized, held):0.##}");
			}
		}


		/// <summary>
		/// Holds the elbow out of the body the way the joint limit holds it inside its cone: it travels its
		/// ring from where it was and stops where the body begins. Never picks a point across the ring.
		/// </summary>
		private Vector3 ClearOfBody(Vector3 centre, float radius, Vector3 axis, Vector3 was, Vector3 roll,
			float offset, out float held)
		{
			held = 0f;
			ArmPath.BodyCapsule[] capsules = BodyCapsules();
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Quaternion inverse = Quaternion.Inverse(body.rotation);

			if (capsules.Length == 0 || radius < 0.0001f ||
				ClearsBody(capsules, centre, radius, roll, offset, body.origin, inverse))
			{
				return roll;
			}

			// The margin is what a swing would LIKE. Where the whole ring is inside the body there is no
			// such point, and refusing to move at all would leave the elbow further in than it need be.
			if (!AnyClear(capsules, centre, radius, axis, was, offset, body.origin, inverse))
			{
				offset = 0f;
			}

			// Where it came from is inside too — the body has turned out from under it. Back to the nearest
			// clear place, which is close, because it only ever strays a little at a time.
			if (!ClearsBody(capsules, centre, radius, was, offset, body.origin, inverse))
			{
				if (!TryNearestClear(capsules, centre, radius, axis, was,
					Vector3.SignedAngle(was, roll, axis) >= 0f ? 1f : -1f, offset,
					body.origin, inverse, out Vector3 back))
				{
					// Nowhere on this ring is clear at all. Leaving it be beats moving it blind.
					return was;
				}
				was = back;
			}

			// The last clear point on the way there. Both ends move smoothly, so this one does too.
			float sweep = Vector3.SignedAngle(was, roll, axis);
			float from = 0f, to = 1f;
			for (int i = 0; i < CLEAR_BISECTIONS; i++)
			{
				float mid = (from + to) * 0.5f;
				if (ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(sweep * mid, axis) * was,
					offset, body.origin, inverse))
				{
					from = mid;
				}
				else
				{
					to = mid;
				}
			}

			Vector3 landed = Quaternion.AngleAxis(sweep * from, axis) * was;
			held = Vector3.Angle(roll, landed);
			return landed;
		}

		/// <summary>
		/// Whether the elbow at <paramref name="direction"/> round its ring is out of the body. The frame
		/// is passed in: the ring is walked a couple of hundred times a frame, and reading it is not free.
		/// </summary>
		private static bool ClearsBody(ArmPath.BodyCapsule[] capsules, Vector3 centre, float radius,
			Vector3 direction, float offset, Vector3 origin, Quaternion inverse)
		{
			Vector3 local = inverse * (centre + direction * radius - origin);

			for (int i = 0; i < capsules.Length; i++)
			{
				if (Vector3.Distance(local, capsules[i].Closest(local)) < capsules[i].Radius + offset)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether any point on the ring at all is clear, which decides if the margin is affordable.</summary>
		private static bool AnyClear(ArmPath.BodyCapsule[] capsules, Vector3 centre, float radius, Vector3 axis,
			Vector3 from, float offset, Vector3 origin, Quaternion inverse)
		{
			for (float turn = 0f; turn < 360f; turn += CLEAR_STEP)
			{
				if (ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(turn, axis) * from,
					offset, origin, inverse))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The nearest point round the ring that is clear of the body, searched outwards from
		/// <paramref name="from"/> so the elbow never crosses to the far side to find one.
		/// </summary>
		private static bool TryNearestClear(ArmPath.BodyCapsule[] capsules, Vector3 centre, float radius,
			Vector3 axis, Vector3 from, float toward, float offset, Vector3 origin, Quaternion inverse,
			out Vector3 nearest)
		{
			nearest = from;
			for (float step = CLEAR_STEP; step <= 180f; step += CLEAR_STEP)
			{
				// The way the elbow was already headed first, so an even split cannot send it backwards.
				for (int side = 0; side < 2; side++)
				{
					float turn = step * (side == 0 ? toward : -toward);
					if (!ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(turn, axis) * from,
						offset, origin, inverse))
					{
						continue;
					}

					// Onto the boundary itself, so it slides as the body turns rather than stepping.
					float inside = turn - Mathf.Sign(turn) * CLEAR_STEP;
					for (int i = 0; i < CLEAR_BISECTIONS; i++)
					{
						float mid = (inside + turn) * 0.5f;
						if (ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(mid, axis) * from,
							offset, origin, inverse))
						{
							turn = mid;
						}
						else
						{
							inside = mid;
						}
					}

					nearest = Quaternion.AngleAxis(turn, axis) * from;
					return true;
				}
			}

			return false;
		}

		/// <summary>How far into the body a world-space joint sits, for logging. Debug only.</summary>
		private float BodyDepth(Vector3 position)
		{
			ArmPath.BodyCapsule[] capsules = BodyCapsules();
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			Vector3 local = Quaternion.Inverse(body.rotation) * (position - body.origin);

			float deepest = 0f;
			for (int i = 0; i < capsules.Length; i++)
			{
				deepest = Mathf.Max(deepest,
					capsules[i].Radius - Vector3.Distance(local, capsules[i].Closest(local)));
			}
			return deepest;
		}

		/// <summary>
		/// Where the elbow leans off the arm's line as things stand, which is what seeds a leg's frame.
		/// Read from the bone, so it is the pose the animation is actually holding.
		/// </summary>
		private Vector3 ElbowLean(ArmState arm, Vector3 shoulder, Vector3 hand)
		{
			Transform joint = Elbow(arm);
			Vector3 line = Line(shoulder, hand);
			Vector3 lean = joint == null
				? Vector3.zero : Vector3.ProjectOnPlane(joint.position - shoulder, line);
			if (lean.sqrMagnitude < 0.000001f)
			{
				lean = Vector3.ProjectOnPlane(BodyFrame().rotation * Vector3.down, line);
			}

			return lean.sqrMagnitude < 0.000001f
				? Vector3.Cross(line, Agent.Transform.up).normalized : lean.normalized;
		}

		/// <summary>The direction from the shoulder to a hand position, safe where the two coincide.</summary>
		private Vector3 Line(Vector3 shoulder, Vector3 hand)
		{
			Vector3 line = hand - shoulder;
			return line.sqrMagnitude < 0.000001f ? Agent.Transform.forward : line.normalized;
		}

		/// <summary>
		/// Logs both hand-posed arms and where their elbows sit on the solution circle, in pelvis space.
		/// Edit-time ground truth for the elbow rule: reads the posed bones only, no dependencies.
		/// </summary>
		[ContextMenu("Log Arm Pose")]
		private void LogArmPose()
		{
			TransformLookup posed = lookup == null ? gameObject.GetComponentRelative<TransformLookup>() : lookup;
			if (posed == null)
			{
				SpaxDebug.Error("ARMPOSE", "No TransformLookup found.");
				return;
			}

			Transform hips = posed.Lookup(HumanBoneIdentifiers.HIPS);
			Transform spineBone = posed.Lookup(HumanBoneIdentifiers.SPINE);
			Transform leftLeg = posed.Lookup(HumanBoneIdentifiers.LEFT_UPPER_LEG);
			Transform rightLeg = posed.Lookup(HumanBoneIdentifiers.RIGHT_UPPER_LEG);
			if (hips == null || spineBone == null || leftLeg == null || rightLeg == null)
			{
				SpaxDebug.Error("ARMPOSE", "Missing pelvis bones.");
				return;
			}

			// The same frame the rule uses: positions only, all three bones rigid to the pelvis.
			Vector3 origin = hips.position;
			Vector3 rise = spineBone.position - hips.position;
			Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(rightLeg.position - leftLeg.position, rise), rise);
			Quaternion inverse = Quaternion.Inverse(rotation);
			Vector3 spine = rotation * Vector3.up;

			Transform chest = posed.Lookup(HumanBoneIdentifiers.UPPER_CHEST);
			if (chest == null)
			{
				chest = posed.Lookup(HumanBoneIdentifiers.CHEST);
			}
			SpaxDebug.Log("ARMPOSE", $"BODY spine {(inverse * (spineBone.position - origin)).ToString("F3")}" +
				$" chest {(chest == null ? "none" : (inverse * (chest.position - origin)).ToString("F3"))}" +
				// How far the chest twists off the pelvis, for the VRIK frame question.
				$" chestTwist {(chest == null ? "none" : (inverse * chest.rotation).eulerAngles.ToString("F1"))}");

			Quaternion chestRotation = chest == null ? rotation : chest.rotation;
			LogPosedArm(posed, true, origin, inverse, spine, chestRotation);
			LogPosedArm(posed, false, origin, inverse, spine, chestRotation);
		}

		/// <summary>
		/// VRIK's bend preference (<c>IKSolverVRArm.GetBendNormal</c>), as a roll vector. Returned before
		/// the cross with <paramref name="dir"/>, which only projects it onto the solution circle.
		/// </summary>
		private static Vector3 VrikBend(Quaternion chest, Vector3 dir, Vector3 armDir, Vector3 palm, Vector3 thumb)
		{
			Quaternion inverse = Quaternion.Inverse(chest);
			Quaternion q = Quaternion.FromToRotation(Vector3.down, inverse * dir.normalized + Vector3.forward);
			Vector3 b = q * Vector3.back;

			q = Quaternion.FromToRotation(inverse * armDir, inverse * dir);
			b = chest * (q * b);

			return b + armDir - palm - thumb * 0.5f;
		}

		/// <summary>The bone-local cardinal axis nearest a world direction — VRIK's hand axis convention.</summary>
		private static Vector3 CardinalAxis(Transform bone, Vector3 direction)
		{
			Vector3 local = Quaternion.Inverse(bone.rotation) * direction;
			float x = Mathf.Abs(local.x), y = Mathf.Abs(local.y), z = Mathf.Abs(local.z);
			if (x >= y && x >= z)
			{
				return new Vector3(Mathf.Sign(local.x), 0f, 0f);
			}
			return y >= z ? new Vector3(0f, Mathf.Sign(local.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
		}

		/// <summary>
		/// One posed arm's joints, its elbow's solution circle, and the elbow's actual place on it
		/// measured against every direction the rule builds from.
		/// </summary>
		private void LogPosedArm(TransformLookup posed, bool isLeft, Vector3 origin, Quaternion inverse, Vector3 spine, Quaternion chest)
		{
			Transform shoulder = posed.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_UPPER_ARM : HumanBoneIdentifiers.RIGHT_UPPER_ARM);
			Transform elbow = posed.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_LOWER_ARM : HumanBoneIdentifiers.RIGHT_LOWER_ARM);
			Transform hand = posed.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			Transform clavicle = posed.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_SHOULDER : HumanBoneIdentifiers.RIGHT_SHOULDER);
			Transform thumbBone = posed.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_THUMB_PROXIMAL : HumanBoneIdentifiers.RIGHT_THUMB_PROXIMAL);

			if (shoulder == null || elbow == null || hand == null ||
				!LimbHint.TryGetCircle(shoulder, elbow, hand, hand.position,
					out Vector3 centre, out float radius, out Vector3 axis))
			{
				SpaxDebug.Error("ARMPOSE", $"{(isLeft ? "LEFT" : "RIGHT")} arm bones missing or degenerate.");
				return;
			}

			// Taken from the posed hand, so the circle passes exactly through the posed elbow.
			Vector3 actual = Vector3.ProjectOnPlane(elbow.position - centre, axis).normalized;
			Vector3 fromSpine = Vector3.ProjectOnPlane(centre - origin, spine);
			Vector3 outward = Vector3.ProjectOnPlane(fromSpine, axis);
			float decides = fromSpine.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(outward.magnitude / fromSpine.magnitude);
			Vector3 hang = Vector3.ProjectOnPlane(-spine, axis).normalized;
			Vector3 level = Vector3.Cross(axis, spine);

			float upper = Vector3.Distance(shoulder.position, elbow.position);
			float fore = Vector3.Distance(elbow.position, hand.position);
			float span = Vector3.Distance(shoulder.position, hand.position);

			SpaxDebug.Log("ARMPOSE", $"{(isLeft ? "LEFT" : "RIGHT")}" +
				$" | shoulder {(inverse * (shoulder.position - origin)).ToString("F3")}" +
				$" elbow {(inverse * (elbow.position - origin)).ToString("F3")}" +
				$" hand {(inverse * (hand.position - origin)).ToString("F3")}" +
				$" clavicle {(clavicle == null ? "none" : (inverse * (clavicle.position - origin)).ToString("F3"))}" +
				$" | upper {upper:0.###} fore {fore:0.###} straight {span / Mathf.Max(upper + fore, 0.0001f):0.###}" +
				$" | centre {(inverse * (centre - origin)).ToString("F3")} radius {radius:0.###} axis {(inverse * axis).ToString("F3")}" +
				$" | actual {(inverse * actual).ToString("F3")}" +
				$" | hang {(inverse * hang).ToString("F3")} outward {(inverse * outward.normalized).ToString("F3")}" +
				$" level {(inverse * level.normalized).ToString("F3")} levelMag {level.magnitude:0.###} decides {decides:0.###}" +
				// Where the posed elbow lies as an angle on its own circle, off each candidate direction.
				$" | fromHang {Vector3.SignedAngle(hang, actual, axis):0.#}" +
				$" fromOut {Vector3.SignedAngle(outward.normalized, actual, axis):0.#}" +
				$" fromLevel {Vector3.SignedAngle(level.normalized, actual, axis):0.#}");

			if (clavicle == null || thumbBone == null)
			{
				SpaxDebug.Error("ARMPOSE", $"{(isLeft ? "LEFT" : "RIGHT")} no clavicle or thumb, skipping VRIK.");
				return;
			}

			// VRIK's own inputs: the clavicle's aim, and the hand's palm and thumb cardinal axes.
			Vector3 dir = hand.position - shoulder.position;
			Vector3 armDir = (shoulder.position - clavicle.position).normalized;
			Vector3 toForearm = elbow.position - hand.position;
			Vector3 handNormal = Vector3.Cross(-toForearm, thumbBone.position - hand.position);
			Vector3 palm = hand.rotation * CardinalAxis(hand, -toForearm);
			Vector3 thumb = hand.rotation * CardinalAxis(hand, Vector3.Cross(handNormal, -toForearm));

			Vector3 byPelvis = Vector3.ProjectOnPlane(VrikBend(Quaternion.Inverse(inverse), dir, armDir, palm, thumb), axis).normalized;
			Vector3 byChest = Vector3.ProjectOnPlane(VrikBend(chest, dir, armDir, palm, thumb), axis).normalized;

			SpaxDebug.Log("ARMPOSE", $"{(isLeft ? "LEFT" : "RIGHT")} VRIK" +
				$" | armDir {(inverse * armDir).ToString("F3")} palm {(inverse * palm).ToString("F3")} thumb {(inverse * thumb).ToString("F3")}" +
				$" | handX {(inverse * (hand.rotation * Vector3.right)).ToString("F3")}" +
				$" handY {(inverse * (hand.rotation * Vector3.up)).ToString("F3")}" +
				$" handZ {(inverse * (hand.rotation * Vector3.forward)).ToString("F3")}" +
				// How far each chest-frame variant lands from the posed elbow, on the circle.
				$" | pelvisFrame {(inverse * byPelvis).ToString("F3")} err {Vector3.SignedAngle(byPelvis, actual, axis):0.#}" +
				$" | chestFrame {(inverse * byChest).ToString("F3")} err {Vector3.SignedAngle(byChest, actual, axis):0.#}");
		}

		/// <summary>
		/// The way the fingers point, in the hand's own space, so a commanded grip rotation gives the
		/// direction directly. Wrist to the knuckles, which is the palm's own length.
		/// </summary>
		private Vector3 FingerLine(ArmState arm)
		{
			Transform handBone = arm.IsLeft ? LeftHand : RightHand;
			Transform knuckle = lookup.Lookup(arm.IsLeft
				? HumanBoneIdentifiers.LEFT_MIDDLE_PROXIMAL : HumanBoneIdentifiers.RIGHT_MIDDLE_PROXIMAL);

			if (handBone == null || knuckle == null)
			{
				return Vector3.zero;
			}

			return Quaternion.Inverse(handBone.rotation) * (knuckle.position - handBone.position).normalized;
		}

		/// <summary>The shoulder's authored reach cone, if one has been put on the upper arm.</summary>
		private RotationLimit JointLimit(ArmState arm)
		{
			Transform bone = Shoulder(arm);
			if (bone == null)
			{
				return null;
			}

			if (!jointLimits.TryGetValue(bone, out RotationLimit limit))
			{
				limit = bone.GetComponent<RotationLimit>();
				jointLimits[bone] = limit;

				// Where the cone actually ended up: its centre is taken from the bone's local rotation
				// when it wakes, so an animated arm at that moment would tilt it without saying so.
				Transform elbowBone = Elbow(arm);
				SpaxDebug.Log("ARMLIMIT", limit == null
					? $"{(arm.IsLeft ? "LEFT" : "RIGHT")} NO RotationLimit on {bone.name} — elbow runs unclamped."
					// The cone lives in the PARENT's space, so its name is what the limit is actually fixed to.
					: $"{(arm.IsLeft ? "LEFT" : "RIGHT")} {limit.GetType().Name} on {bone.name}" +
						$" fixed to {(bone.parent == null ? "NOTHING" : bone.parent.name)} | axis {limit.axis}" +
						$" measured {(elbowBone == null ? Vector3.zero : Quaternion.Inverse(bone.rotation) * (elbowBone.position - bone.position).normalized)}" +
						$" | cone centre {(bone.parent == null ? Vector3.zero : Quaternion.Inverse(BodyFrame().rotation) * (bone.parent.rotation * limit.defaultLocalRotation * limit.axis))}" +
						$" (outward is {(arm.IsLeft ? Vector3.left : Vector3.right)})");
			}
			return limit;
		}

		/// <summary>Whether the shoulder can actually put its elbow there.</summary>
		private bool AllowsElbow(RotationLimit limit, Transform shoulder, Vector3 elbow)
		{
			Vector3 humerus = (elbow - shoulder.position).normalized;
			Quaternion aimed = Quaternion.FromToRotation(shoulder.rotation * limit.axis, humerus) * shoulder.rotation;
			Quaternion allowed = limit.GetLimitedLocalRotation(
				Quaternion.Inverse(shoulder.parent.rotation) * aimed, out _);

			// By where the axis ends up, not by whether anything changed: a twist limit can change the
			// rotation without moving the elbow at all, and only the elbow is being asked about.
			return Vector3.Angle(shoulder.parent.rotation * allowed * limit.axis, humerus) < 0.5f;
		}

		/// <summary>
		/// The elbow held inside the joint limit by travelling its own ring from where it already was, so
		/// it stops at the edge of what the shoulder allows instead of being teleported to the far side
		/// of the cone when the nearest allowed pose jumps.
		/// </summary>
		private Vector3 HoldWithinLimit(ArmState arm, Transform shoulder, Vector3 was, Vector3 roll,
			Vector3 centre, float radius, Vector3 axis, out float clamped, out string mode)
		{
			clamped = 0f;
			mode = "free";
			RotationLimit limit = JointLimit(arm);
			if (limit == null || limit.axis == Vector3.zero || shoulder.parent == null ||
				AllowsElbow(limit, shoulder, centre + roll * radius))
			{
				return roll;
			}

			// Where it came from is not allowed either — the ring has turned out from under it. Back to the
			// nearest allowed place, which is close, because it only ever strays a little at a time.
			if (!AllowsElbow(limit, shoulder, centre + was * radius))
			{
				mode = "RECOVER";
				if (!TryNearestAllowed(limit, shoulder, centre, radius, axis, was,
					Vector3.SignedAngle(was, roll, axis) >= 0f ? 1f : -1f, out Vector3 back))
				{
					// Nowhere on this ring is allowed at all. Leaving it be beats moving it blind.
					return was;
				}
				was = back;
			}
			else
			{
				mode = "edge";
			}

			// The last allowed point on the way there. Both ends move smoothly, so this one does too.
			float sweep = Vector3.SignedAngle(was, roll, axis);
			float from = 0f, to = 1f;
			for (int i = 0; i < 8; i++)
			{
				float mid = (from + to) * 0.5f;
				if (AllowsElbow(limit, shoulder, centre + (Quaternion.AngleAxis(sweep * mid, axis) * was) * radius))
				{
					from = mid;
				}
				else
				{
					to = mid;
				}
			}

			Vector3 landed = Quaternion.AngleAxis(sweep * from, axis) * was;
			clamped = Vector3.Angle(roll, landed);
			return landed;
		}

		/// <summary>
		/// The nearest direction on the ring the shoulder allows, stepped out from where the elbow already
		/// is, so the answer is only ever as far off as the elbow actually strayed.
		/// </summary>
		private bool TryNearestAllowed(RotationLimit limit, Transform shoulder, Vector3 centre, float radius,
			Vector3 axis, Vector3 from, float toward, out Vector3 nearest)
		{
			const float STEP = 2f;

			nearest = from;
			for (float step = STEP; step <= 180f; step += STEP)
			{
				// The way the elbow was already headed first, so an even split cannot send it backwards.
				for (int side = 0; side < 2; side++)
				{
					float turn = step * (side == 0 ? toward : -toward);
					if (!AllowsElbow(limit, shoulder, centre + (Quaternion.AngleAxis(turn, axis) * from) * radius))
					{
						continue;
					}

					// Onto the boundary itself, so it slides as the ring turns rather than stepping.
					float outside = turn - Mathf.Sign(turn) * STEP;
					for (int i = 0; i < 6; i++)
					{
						float mid = (outside + turn) * 0.5f;
						if (AllowsElbow(limit, shoulder, centre + (Quaternion.AngleAxis(mid, axis) * from) * radius))
						{
							turn = mid;
						}
						else
						{
							outside = mid;
						}
					}

					nearest = Quaternion.AngleAxis(turn, axis) * from;
					return true;
				}
			}

			return false;
		}

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

		private void FinishLeg(ArmTransition transition, Vector3 position, Quaternion rotation)
		{
			// Whatever comes next starts from exactly where this leg landed.
			Freeze(transition, position, rotation);

			switch (transition.Leg)
			{
				case TransitionLeg.ToStow:
					// The hand has arrived at the resting place: let go.
					transition.Arm.Wielded = null;
					transition.Stowing.Unwield();
					Stow(transition.Arm, transition.Stowing);
					ArmChangedEvent?.Invoke(transition.Arm);

					BeginLeg(transition, TransitionLeg.ToDraw);
					break;

				case TransitionLeg.ToDraw:
					if (transition.Drawing != null)
					{
						transition.Arm.Wielded = transition.Drawing;
						Draw(transition.Arm, transition.Drawing);
						transition.Drawing.Wield();
						ArmChangedEvent?.Invoke(transition.Arm);
					}
					StowInactive(transition.Arm);
					UpdateSheathedFlag();

					BeginLeg(transition, TransitionLeg.Recover);
					break;

				case TransitionLeg.Recover:
					ik.RemoveInfluencer(this, transition.Arm.IKChain);
					ik.RemoveHintInfluencer(this, transition.Arm.IKChain);
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
					transition.Clearance = HandRadius(transition.Arm);
					break;

				case TransitionLeg.ToDraw:
					if (transition.Drawing == null)
					{
						// Nothing to pick up — the stow already happened, so head home.
						StowInactive(transition.Arm);
						UpdateSheathedFlag();
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
					transition.Clearance = HandRadius(transition.Arm);
					break;

				case TransitionLeg.Recover:
					// Draw clear of the sheathe, then swing home to where the animation left the hand.
					transition.From = null;
					transition.To = null;
					transition.Blend = LegBlend.Out;

					// Only something actually in hand has to come out first; an empty hand just leaves.
					SetSlide(transition, transition.Drawing, null);
					transition.Clearance = HandRadius(transition.Arm);
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
			return (Quaternion.Inverse(Agent.Transform.rotation) * -approach,
				Mathf.Min(distance, ReachLimit(arm, grip, approach)));
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

			if (debugPath)
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
					$" | slotFrom {SlotInfo(transition.From)} slotTo {SlotInfo(transition.To)}");
			}
		}

		/// <summary>A bone's height in the torso's frame, for logging. Debug only.</summary>
		private float Height(Transform bone)
		{
			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return bone == null ? float.NaN : (Quaternion.Inverse(body.rotation) * (bone.position - body.origin)).y;
		}

		/// <summary>Where an armament's own resting anchor sits in agent space, for logging.</summary>
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

		private string SlotInfo(RuntimeEquipedData data)
		{
			if (data == null)
			{
				return "-";
			}

			bool isLeft = false;
			for (int i = 0; i < leftArm.Armaments.Count; i++)
			{
				isLeft |= leftArm.Armaments[i] == data;
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			return (Quaternion.Inverse(body.rotation) *
				(GetRestOrientation(isLeft ? leftArm : rightArm, data).pos - body.origin)).ToString();
		}

		/// <summary>
		/// How far past the body a swing rides: the hand's own collision sphere, and nothing else. What it
		/// grips sits inside that sphere, so it has nothing of its own to clear.
		/// </summary>
		private float HandRadius(ArmState arm)
		{
			if (SafeBody == null || !SafeBody.TryGetBoneColliders(
				arm.IsLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND,
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
		/// </summary>
		private void SampleAnimatedHands(float delta)
		{
			if (LeftHand != null)
			{
				leftAnimated = Localize(LeftHand.position, LeftHand.rotation);
				leftSwivel = AnimatedSwivel(leftArm, LeftHand);
			}
			if (RightHand != null)
			{
				rightAnimated = Localize(RightHand.position, RightHand.rotation);
				rightSwivel = AnimatedSwivel(rightArm, RightHand);
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

			Quaternion inverse = Quaternion.Inverse(Agent.Transform.rotation);
			return (inverse * axis, inverse * roll.normalized);
		}

		/// <summary>
		/// The elbow the animation would have if its arm were turned to point where this one does. The
		/// arm's own swivel carried across rather than measured afresh against anything in the world.
		/// </summary>
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
			transition.HomeTwist = Quaternion.Inverse(Agent.Transform.rotation) * BodyFrame().rotation;
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

			return (Quaternion.Inverse(Agent.Transform.rotation) * axis, depth);
		}

		/// <summary>
		/// Shoulder-to-hand length of an arm, measured live so it follows rig scale.
		/// </summary>
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

		/// <summary>
		/// Puts this arm's intended armament in hand and everything else away, with no animation.
		/// </summary>
		private void ApplyArm(ArmState arm)
		{
			RuntimeEquipedData target = Intended(arm);

			if (arm.Wielded != target)
			{
				if (arm.Wielded != null)
				{
					arm.Wielded.Unwield();
					Stow(arm, arm.Wielded);
				}

				arm.Wielded = target;

				if (target != null)
				{
					Draw(arm, target);
					target.Wield();
				}

				ArmChangedEvent?.Invoke(arm);
			}

			StowInactive(arm);
		}

		/// <summary>Every armament this arm isn't holding rests visibly on the body.</summary>
		private void StowInactive(ArmState arm)
		{
			for (int i = 0; i < arm.Armaments.Count; i++)
			{
				RuntimeEquipedData data = arm.Armaments[i];
				if (data == null || data == arm.Wielded)
				{
					continue;
				}

				data.Unwield();
				Stow(arm, data);
			}
		}

		private void EndTransition()
		{
			if (ik != null)
			{
				ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
				ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);
				ik.RemoveHintInfluencer(this, IKChainConstants.LEFT_ARM);
				ik.RemoveHintInfluencer(this, IKChainConstants.RIGHT_ARM);
			}

			if (controlMod != null)
			{
				if (rigidbodyWrapper != null)
				{
					rigidbodyWrapper.Control.RemoveModifier(this);
				}
				controlMod.Dispose();
				controlMod = null;
			}

			transitions.Clear();
			pendingSwaps.Clear();
			active = false;
			RunTime = 0f;
			Act = null;
			Canceled = false;
			CancelTime = 0f;
			State = PerformanceState.Inactive;
		}

		private void UpdateSheathedFlag()
		{
			bool sheathed = desiredSheathed && leftArm.Wielded == null && rightArm.Wielded == null;
			if (Sheathed == sheathed)
			{
				return;
			}

			Sheathed = sheathed;
			SheathedEvent?.Invoke(Sheathed);
		}

		private static bool IsTwoHanded(RuntimeEquipedData data)
		{
			return data != null &&
				data.RuntimeItemData.RuntimeData.TryGetValue(ItemDataIdentifiers.TWO_HANDED, out bool twoHanded) &&
				twoHanded;
		}

		#endregion Transition

		#region IPerformer

		/// <inheritdoc/>
		public bool SupportsAct(string act)
		{
			return act == ActorActs.SHEATHE || act == ActorActs.SWAP_LEFT || act == ActorActs.SWAP_RIGHT;
		}

		/// <inheritdoc/>
		public bool TryPrepare(IAct act, out IPerformer performer)
		{
			performer = null;

			if (!SupportsAct(act.Title))
			{
				return false;
			}

			// An act only claims the arms it touches, so the other arm stays free to accept its own.
			if (act.Title == ActorActs.SHEATHE)
			{
				bool started = false;
				started |= TryStartSheathe(leftArm);
				started |= TryStartSheathe(rightArm);
				if (!started)
				{
					return false;
				}
			}
			else
			{
				ArmState arm = GetArm(act.Title == ActorActs.SWAP_LEFT);
				if (!Claimable(arm))
				{
					return false;
				}

				// Hold time decides cycle-vs-unarm, so nothing moves until the button resolves.
				pendingSwaps.Add(new PendingSwap { Arm = arm });
			}

			Act = act;
			Canceled = false;
			CancelTime = 0f;
			State = PerformanceState.Preparing;

			if (!active)
			{
				active = true;
				RunTime = 0f;
				AddControlModifier();
			}

			performer = this;
			StartedPreparingEvent?.Invoke(this);
			return true;

			bool TryStartSheathe(ArmState arm)
			{
				if (Intended(arm) == arm.Wielded || !Claimable(arm))
				{
					return false;
				}
				StartTransition(arm);
				return true;
			}
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (pendingSwaps.Count > 0)
			{
				// Released before the hold threshold: move to the next armament. The newest pending swap
				// is the one being released — the Actor only routes a release whose press was the last to land.
				CommitSwap(pendingSwaps[pendingSwaps.Count - 1], false);
				return true;
			}

			return State == PerformanceState.Preparing || State == PerformanceState.Performing;
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force = false)
		{
			if (!active || !force)
			{
				// Never abandon a transition halfway — the armament would be left parented to the wrong place.
				return false;
			}

			foreach (ArmTransition transition in transitions)
			{
				ApplyArm(transition.Arm);
			}
			UpdateSheathedFlag();

			Canceled = true;
			State = PerformanceState.Completed;
			PerformanceCompletedEvent?.Invoke(this);
			EndTransition();
			return true;
		}

		/// <summary>
		/// Resolves a held swap and starts that arm moving. Nothing moves when everything is stowed —
		/// the swap only changes which armament is next out, which the stack order shows.
		/// </summary>
		private void CommitSwap(PendingSwap pending, bool unarm)
		{
			pendingSwaps.Remove(pending);

			if (unarm)
			{
				UnarmArm(pending.Arm);
			}
			else
			{
				CycleArm(pending.Arm);
			}

			StartTransition(pending.Arm);
			StartedPerformingEvent?.Invoke(this);
		}

		private void AddControlModifier()
		{
			if (transitionControl < 1f && rigidbodyWrapper != null && controlMod == null)
			{
				// Starts neutral — UpdateControl fades it in with the arm, so adding it is not felt.
				controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				rigidbodyWrapper.Control.AddModifier(this, controlMod);
			}
		}

		/// <summary>
		/// Ties movement control to how far the arm is actually committed, so it eases off and back on
		/// with the reach instead of snapping the moment a swap starts and ends.
		/// </summary>
		private void UpdateControl()
		{
			if (controlMod == null)
			{
				return;
			}

			float engaged = 0f;
			foreach (ArmTransition transition in transitions)
			{
				engaged = Mathf.Max(engaged, transition.Commitment);
			}

			controlMod.SetValue(Mathf.Lerp(1f, transitionControl, engaged));
		}

		#endregion IPerformer

		#region Orientation

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
			Transform grip = GripOf(subject);
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

			if (drawGizmos)
			{
				Debug.DrawLine(hand.position, anchorPos, Color.yellow);
			}

			return orientation;
		}

		/// <summary>Where the wielding hand grips this armament, or null when its root is the grip.</summary>
		private static Transform GripOf(RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			return carryable == null ? null : carryable.MainHand;
		}

		/// <summary>
		/// Moves <paramref name="root"/> so that <paramref name="grip"/> lands exactly on the target.
		/// Done in world space, so no scale anywhere in either chain can distort it.
		/// </summary>
		private static void AlignGrip(Transform root, Transform grip, Vector3 targetPos, Quaternion targetRot)
		{
			if (grip == null)
			{
				grip = root;
			}

			// Rotate first, then close whatever gap the rotation left.
			root.rotation = targetRot * Quaternion.Inverse(grip.rotation) * root.rotation;
			root.position += targetPos - grip.position;
		}

		/// <summary>
		/// The armament this arm would bring out next: the active one, or — while unarmed — the one it
		/// last held, since that is what re-arming returns to.
		/// </summary>
		private static RuntimeEquipedData NextOut(ArmState arm)
		{
			if (arm.Active != null)
			{
				return arm.Active;
			}

			return arm.LastActiveIndex >= 0 && arm.LastActiveIndex < arm.Armaments.Count
				? arm.Armaments[arm.LastActiveIndex]
				: null;
		}

		/// <summary>
		/// Where this armament sits in its point's stack. The next one out leads, so the body always shows
		/// what the arm will reach for — readable even while unarmed or sheathed.
		/// </summary>
		private static int StowOrder(ArmState arm, RuntimeEquipedData data)
		{
			if (data != null && data == NextOut(arm))
			{
				return 0;
			}

			for (int i = 0; i < arm.Armaments.Count; i++)
			{
				if (arm.Armaments[i] == data)
				{
					return i + 1;
				}
			}
			return int.MaxValue - 1;
		}

		/// <summary>Re-sorts this arm's stowed armaments after its active slot changed.</summary>
		private void RefreshStowOrder(ArmState arm)
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
					sheathe.TryAssign(data, arm.Side, StowOrder(arm, data));
				}
			}
		}

		/// <summary>Rests an armament at its sheathe point, or on the arm's plain sheathe transform.</summary>
		private void Stow(ArmState arm, RuntimeEquipedData data)
		{
			if (data == null || data.EquipedInstance == null)
			{
				return;
			}

			if (sheathe != null)
			{
				sheathe.TryAssign(data, arm.Side, StowOrder(arm, data));
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

		/// <summary>Takes an armament off the body and aligns its grip to the hand.</summary>
		private void Draw(ArmState arm, RuntimeEquipedData data)
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
			Transform grip = GripOf(data);

			if (debugPath)
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
			AlignGrip(transform, grip, slot.pos, slot.rot);
		}

		#endregion Orientation

		#region Equipment

		private void OnEquip(ArmState arm, int index, RuntimeEquipedData data)
		{
			arm.SetArmament(index, data);

			// First thing into an empty arm becomes what it reaches for.
			if (arm.ActiveIndex < 0 && arm.Wielded == null)
			{
				SetActiveSlot(arm, index);
			}

			// Land it wherever this arm currently has it: in hand if it is the intended one, else on the body.
			if (Intended(arm) == data)
			{
				arm.Wielded = data;
				Draw(arm, data);
				data.Wield();
			}
			else
			{
				data.Unwield();
				Stow(arm, data);
			}

			ArmChangedEvent?.Invoke(arm);
		}

		private void OnUnequip(ArmState arm, int index, RuntimeEquipedData data)
		{
			arm.SetArmament(index, null);

			if (arm.Wielded == data)
			{
				arm.Wielded = null;
			}
			if (arm.ActiveIndex == index)
			{
				arm.ActiveIndex = -1;
			}

			if (sheathe != null)
			{
				sheathe.Release(data);
			}

			// Drop any leg that was reaching for it.
			for (int i = transitions.Count - 1; i >= 0; i--)
			{
				if (transitions[i].Stowing == data || transitions[i].Drawing == data)
				{
					if (ik != null)
					{
						ik.RemoveInfluencer(this, transitions[i].Arm.IKChain);
						ik.RemoveHintInfluencer(this, transitions[i].Arm.IKChain);
					}
					transitions.RemoveAt(i);
				}
			}

			ArmChangedEvent?.Invoke(arm);
		}

		#endregion Equipment

		#region Gizmos

		protected void OnDrawGizmos()
		{
			if (drawGizmos)
			{
				DrawHandSlotGizmos();
				DrawBodyProfile();
			}

			DrawSwapPaths();
		}

		/// <summary>
		/// The capsules the arc is actually measured against, drawn where the code reads them rather than
		/// where the collider gizmo puts them. Worth seeing whenever a hand clips something it "cleared".
		/// </summary>
		private void DrawBodyProfile()
		{
			if (lookup == null)
			{
				return;
			}

			(Vector3 origin, Quaternion rotation) body = BodyFrame();
			ArmPath.BodyCapsule[] capsules = BodyCapsules();
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

		/// <summary>
		/// The route each hand is taking, for as long as it is taking it. Drawn from the same path the
		/// hand is actually driven by, rebuilt here, so it cannot show a route the hand is not on.
		/// </summary>
		private void DrawSwapPaths()
		{
			if (transitions == null || lookup == null)
			{
				return;
			}

			foreach (ArmTransition transition in transitions)
			{
				if (transition.Leg == TransitionLeg.Done)
				{
					continue;
				}

				ArmPath path = BuildPath(transition);
				Gizmos.color = Color.red;

				path.Place(0f, out Vector3 previous);
				for (int i = 1; i <= PATH_GIZMO_SAMPLES; i++)
				{
					path.Place(i / (float)PATH_GIZMO_SAMPLES, out Vector3 point);
					Gizmos.DrawLine(previous, point);
					previous = point;
				}

				// Where the slides end and the swing begins, and how far along the hand is right now.
				Gizmos.DrawWireCube(path.WithdrawPoint, Vector3.one * 0.02f);
				Gizmos.DrawWireCube(path.PreInsertPoint, Vector3.one * 0.02f);

				float progress = transition.Duration <= 0f ? 1f : Mathf.Clamp01(transition.Time / transition.Duration);
				path.Place(progress.InOutCubic(), out Vector3 at);
				Gizmos.DrawWireSphere(at, 0.025f);
			}
		}

		private void DrawHandSlotGizmos()
		{
			if (lookup == null)
			{
				lookup = gameObject.GetComponentRelative<TransformLookup>();
			}
			if (lookup != null)
			{
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
		}

		#endregion Gizmos

		private enum TransitionLeg { None, ToStow, ToDraw, Recover, Done }

		/// <summary>How a leg takes the hand off the animation and hands it back.</summary>
		private enum LegBlend { In, Hold, Out }

		/// <summary>A swap whose button is still down, counting toward the unarm threshold.</summary>
		private class PendingSwap
		{
			public ArmState Arm;
			public float Time;
		}

		/// <summary>One arm's journey: put away what it holds, pick up what it should, come home.</summary>
		private class ArmTransition
		{
			public ArmState Arm;
			public RuntimeEquipedData Stowing;
			public RuntimeEquipedData Drawing;

			/// <summary>
			/// How <see cref="Drawing"/> slides clear of its OWN sheathe, captured before the transition
			/// touches anything. By the time Recover needs it, <see cref="Drawing"/> has already been
			/// pulled from the sheathe registry and reoriented to the grip, so asking then reads that
			/// instead — the withdraw has to be measured while it is still true.
			/// </summary>
			public Vector3 WithdrawAxis;
			public float WithdrawDepth;

			public TransitionLeg Leg;
			public float Time;
			public float Duration;
			public LegBlend Blend;

			/// <summary>Endpoints of the current leg. Null start means the frozen pose, null end means home.</summary>
			public RuntimeEquipedData From;
			public RuntimeEquipedData To;

			/// <summary>The straight slide out of and into a sheathe, in agent space.</summary>
			public Vector3 StartAxis;
			public float StartDepth;
			public Vector3 EndAxis;
			public float EndDepth;

			/// <summary>Which ends took the over-shoulder way in rather than an armament's own slide. Debug only.</summary>
			public bool StartGated;
			public bool EndGated;

			/// <summary>Which side of the body the swing rides, settled with the leg for the same reason:
			/// re-deciding it mid-swing mirrors the arc and throws the hand across the body.</summary>
			public bool ArcFront;
			public float ArcSide;

			/// <summary>Which way round the leftover roll turns: below zero the long way. Settled too, or
			/// a live end pose flips it mid-swing and the hand reverses on itself.</summary>
			public float Turning;

			/// <summary>Whether each end sits on the far side of the spine from the arm reaching for it.</summary>
			public bool StartFar;
			public bool EndFar;

			/// <summary>Whether either end of this leg is over the shoulder. The swing may only rise to
			/// shoulder height when it is, and passes under the joint when it is not.</summary>
			public bool ReachesOver;


			/// <summary>What the hand holds on this leg: whether it holds anything, which way that is
			/// headed, and whether its sheathe is over the shoulder.</summary>
			public bool Carrying;
			public bool CarriedIn;
			public bool TurnsOver;

			/// <summary>The animation's elbow direction and the arm line it was sampled on, both PINNED when
			/// the swap latched, and that pair turned onto the line the arm has now. Agent space.</summary>
			public Vector3 SwivelSeed;
			public Vector3 SwivelAxis;
			public Vector3 SwivelRoll;
			public bool HasSwivel;

			/// <summary>Last frame's elbow, in agent space. Only the joint limit reads it, as a place the
			/// elbow is known to have been allowed to be.</summary>
			public Vector3 ElbowRoll;
			public bool HasElbowRoll;

			/// <summary>Share of this leg's time spent sliding rather than swinging.</summary>
			public float WithdrawFraction;
			public float InsertFraction;

			/// <summary>How far past the skin the middle of this leg's arc rides.</summary>
			public float Clearance;

			/// <summary>IK influence applied this frame.</summary>
			public float Weight;

			/// <summary>How far out on a limb this leg has the arm. Movement control follows it.</summary>
			public float Commitment;

			/// <summary>Hand-over point, in agent space so it follows the body.</summary>
			public Vector3 FrozenPosition;
			public Quaternion FrozenRotation;

			/// <summary>Where the animation had the hand when the arm was claimed — what recovery returns to.</summary>
			public Vector3 HomePosition;
			public Quaternion HomeRotation;

			/// <summary>And where it had the elbow, as an angle from hanging on its solution circle.</summary>

			/// <summary>The torso's twist against the root when the arm was claimed. Debug only.</summary>
			public Quaternion HomeTwist;
		}

		/// <summary>
		/// Everything one arm is carrying and which of it is in hand. Handed whole to
		/// <see cref="ArmChangedEvent"/> so UI can render an arm without querying back.
		/// </summary>
		public class ArmState
		{
			public bool IsLeft { get; }
			public ArmSide Side { get; }
			public string IKChain { get; }

			/// <summary>This arm's equipment slots, in cycle order.</summary>
			public IReadOnlyList<IEquipmentSlot> Slots => slots;

			/// <summary>What sits in each slot. Index-aligned with <see cref="Slots"/>; null means empty.</summary>
			public IReadOnlyList<RuntimeEquipedData> Armaments => armaments;

			/// <summary>Which slot this arm reaches for. -1 means unarmed.</summary>
			public int ActiveIndex { get; internal set; } = -1;

			/// <summary>The slot to return to when re-arming after going unarmed.</summary>
			public int LastActiveIndex { get; internal set; } = -1;

			/// <summary>The armament this arm wants in hand, sheathing aside.</summary>
			public RuntimeEquipedData Active =>
				ActiveIndex >= 0 && ActiveIndex < armaments.Count ? armaments[ActiveIndex] : null;

			/// <summary>The armament actually in hand right now, if any.</summary>
			public RuntimeEquipedData Wielded { get; internal set; }

			private readonly List<IEquipmentSlot> slots = new List<IEquipmentSlot>();
			private readonly List<RuntimeEquipedData> armaments = new List<RuntimeEquipedData>();

			public ArmState(bool isLeft)
			{
				IsLeft = isLeft;
				Side = isLeft ? ArmSide.Left : ArmSide.Right;
				IKChain = isLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;
			}

			internal void AddSlot(IEquipmentSlot slot)
			{
				slots.Add(slot);
				armaments.Add(null);
			}

			internal void SetArmament(int index, RuntimeEquipedData data)
			{
				if (index >= 0 && index < armaments.Count)
				{
					armaments[index] = data;
				}
			}
		}
	}
}
