using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component which supplies the arms' equipment slots and owns what each arm is doing:
	/// which armament is active, whether it is drawn, and the transition between.
	/// The performer only: it decides WHAT should happen and WHEN, and asks an
	/// <see cref="ArmSwapAnimator"/> to actually move a hand — that class owns everything about HOW.
	/// </summary>
	public class AgentArmsComponent : AgentComponentBase, IPerformer
	{
		/// <summary>How finely a swap's route is drawn. Enough that the arc reads as a curve.</summary>
		private const int PATH_GIZMO_SAMPLES = 48;

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

		/// <summary>
		/// The animator once dependency injection has run, or a throwaway instance built from the same
		/// edit-mode fallbacks as <see cref="SafeLookup"/>/<see cref="SafeBody"/> before it has.
		/// Never cached when thrown together this way — caching a pre-DI instance would freeze it with
		/// null IK and sheathe references even after the real ones arrive.
		/// </summary>
		private ArmSwapAnimator SafeAnimator => animator ?? new ArmSwapAnimator(
			this, SafeLookup, SafeBody, Agent.Transform, null, null, ikPriority,
			handSpeed, minLegDuration, arcBulge, gripEasing);

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

		private TransformLookup lookup;
		private EquipmentComponent equipment;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;
		private AgentSheatheComponent sheathe;
		private IAgentBody agentBody;
		private EntityStat entityTimeScale;

		/// <summary>Everything about HOW a hand gets where it is going. Built once DI has run.</summary>
		private ArmSwapAnimator animator;

		private ArmState leftArm;
		private ArmState rightArm;

		private bool desiredSheathed = true;

		/// <summary>Whether this performer is registered with the Actor right now.</summary>
		private bool active;

		private readonly List<PendingSwap> pendingSwaps = new List<PendingSwap>();
		private readonly List<ArmTransition> transitions = new List<ArmTransition>();
		private FloatOperationModifier controlMod;

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

			animator = new ArmSwapAnimator(this, lookup, agentBody, Agent.Transform, ik, sheathe,
				ikPriority, handSpeed, minLegDuration, arcBulge, gripEasing);
			animator.StowReached += OnStowReached;
			animator.DrawReached += OnDrawReached;
			animator.ArmSettled += OnArmSettled;
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
			(Vector3 pos, Quaternion rot) orientation = SafeAnimator.GetHandSlotOrientation(isLeft, false,
				weapon == null ? AgentSheatheComponent.DEFAULT_WIELD_RADIUS : weapon.WieldRadius);
			ArmUtils.AlignGrip(instance.transform, weapon == null ? null : weapon.MainHand, orientation.pos, orientation.rot);
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
						ArmUtils.SlotID(arm.IsLeft, index), type,
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
			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, _ => animator.SampleAnimatedHands(leftArm, rightArm));
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

		public ArmState GetArm(bool isLeft)
		{
			return isLeft ? leftArm : rightArm;
		}

		/// <inheritdoc cref="ArmSwapAnimator.GetHandSlotOrientation"/>
		public (Vector3 pos, Quaternion rot) GetHandSlotOrientation(bool isLeft, bool local,
			float wieldRadius = AgentSheatheComponent.DEFAULT_WIELD_RADIUS)
		{
			return SafeAnimator.GetHandSlotOrientation(isLeft, local, wieldRadius);
		}

		/// <inheritdoc cref="ArmSwapAnimator.ArmLength"/>
		public float ArmLength(bool isLeft)
		{
			return SafeAnimator.ArmLength(isLeft);
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
				if (other.ActiveIndex >= 0 && (ArmUtils.IsTwoHanded(arm.Active) || ArmUtils.IsTwoHanded(other.Active)))
				{
					other.ActiveIndex = -1;
					animator.RefreshStowOrder(other);
					ArmChangedEvent?.Invoke(other);
				}
			}

			// The stack shows which armament is next out, so it re-sorts even when nothing is drawn.
			animator.RefreshStowOrder(arm);
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

		#region Performance

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
			// Cheap enough to sync every frame, and it means an inspector toggle takes effect immediately.
			animator.DebugLogging = debugPath;
			animator.DrawGizmos = drawGizmos;

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
			transitions.Add(animator.Begin(arm, arm.Wielded, target));
		}

		/// <summary>
		/// Advances every arm's leg. Each leg's length is the distance the hand actually covers, so a
		/// swap between two armaments sharing a resting place costs almost nothing.
		/// </summary>
		private void RunLegs(float delta)
		{
			for (int i = transitions.Count - 1; i >= 0; i--)
			{
				if (animator.Advance(transitions[i], delta, Paused))
				{
					transitions.RemoveAt(i);
				}
			}
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
					animator.Stow(arm, arm.Wielded);
				}

				arm.Wielded = target;

				if (target != null)
				{
					animator.Draw(arm, target);
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
				animator.Stow(arm, data);
			}
		}

		private void EndTransition()
		{
			if (ik != null)
			{
				animator.ReleaseAll();
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

		/// <summary>The animator's leg landed the item at rest: let go of it.</summary>
		private void OnStowReached(ArmState arm, RuntimeEquipedData data)
		{
			arm.Wielded = null;
			data.Unwield();
			animator.Stow(arm, data);
			ArmChangedEvent?.Invoke(arm);
		}

		/// <summary>The animator's leg landed the grip in hand: it is wielded from here.</summary>
		private void OnDrawReached(ArmState arm, RuntimeEquipedData data)
		{
			arm.Wielded = data;
			animator.Draw(arm, data);
			data.Wield();
			ArmChangedEvent?.Invoke(arm);
		}

		/// <summary>The arm has nothing left to reach for this swap: rest what is not wielded.</summary>
		private void OnArmSettled(ArmState arm)
		{
			StowInactive(arm);
			UpdateSheathedFlag();
		}

		#endregion Performance

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
				animator.Draw(arm, data);
				data.Wield();
			}
			else
			{
				data.Unwield();
				animator.Stow(arm, data);
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
			ArmSwapAnimator anim = SafeAnimator;
			anim.DebugLogging = debugPath;
			anim.DrawGizmos = drawGizmos;

			if (drawGizmos)
			{
				anim.DrawHandSlotGizmos();
				anim.DrawBodyProfile();
			}

			DrawSwapPaths(anim);
		}

		/// <summary>
		/// The route each hand is taking, for as long as it is taking it. Drawn from the same path the
		/// hand is actually driven by, rebuilt here, so it cannot show a route the hand is not on.
		/// </summary>
		private void DrawSwapPaths(ArmSwapAnimator anim)
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

				ArmPath path = anim.BuildPath(transition);
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

		#endregion Gizmos
	}
}
