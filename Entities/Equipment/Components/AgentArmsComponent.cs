using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component which supplies the arms' equipment slots and owns what each arm is doing:
	/// which armament is active, whether it is drawn, and the transition between.
	/// </summary>
	public class AgentArmsComponent : AgentComponentBase, IPerformer
	{
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

		[Header("DEBUGGING")]
		[SerializeField] private GameObject testPrefab;
		[SerializeField] private bool testLeft;
		[SerializeField] private bool testRight;

		private TransformLookup lookup;
		private EquipmentComponent equipment;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;
		private AgentSheatheComponent sheathe;
		private EntityStat entityTimeScale;

		private ArmState leftArm;
		private ArmState rightArm;

		private bool desiredSheathed = true;
		private TransitionPhase phase;
		private float decideTime;
		private ArmState pendingSwapArm;
		private readonly List<ArmTransition> transitions = new List<ArmTransition>();
		private FloatOperationModifier controlMod;

		public void InjectDependencies(IEntity entity, EquipmentComponent equipment, TransformLookup lookup,
			CallbackService callbackService, [Optional] IIKComponent ik, [Optional] RigidbodyWrapper rigidbodyWrapper,
			[Optional] AgentSheatheComponent sheathe)
		{
			this.equipment = equipment;
			this.lookup = lookup;
			this.callbackService = callbackService;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.sheathe = sheathe;

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
		}

		protected void OnDisable()
		{
			callbackService.UnsubscribeUpdates(this);

			if (phase != TransitionPhase.None)
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
					ArmChangedEvent?.Invoke(other);
				}
			}

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

		private bool NeedsTransition()
		{
			return Intended(leftArm) != leftArm.Wielded || Intended(rightArm) != rightArm.Wielded;
		}

		private void OnUpdate(float delta)
		{
			if (phase == TransitionPhase.None)
			{
				Reconcile();
				return;
			}

			float scaledDelta = delta * (entityTimeScale ?? 1f);
			if (!Paused)
			{
				RunTime += scaledDelta;
			}

			switch (phase)
			{
				case TransitionPhase.Deciding:
					if (!Paused)
					{
						decideTime += scaledDelta;
					}
					// Held long enough to mean "put it away" rather than "next one".
					if (decideTime >= unarmThreshold)
					{
						CommitSwap(true);
					}
					break;

				case TransitionPhase.Running:
					RunLegs(scaledDelta);
					break;
			}

			PerformanceUpdateEvent?.Invoke(this);

			if (State == PerformanceState.Completed)
			{
				PerformanceCompletedEvent?.Invoke(this);
				EndTransition();
			}
		}

		/// <summary>
		/// Drives actual state toward desired. Runs every frame the arms are idle, so a request that
		/// couldn't be honoured earlier is honoured as soon as it can be.
		/// </summary>
		private void Reconcile()
		{
			if (!NeedsTransition() ||
				(State != PerformanceState.Inactive && State != PerformanceState.Completed))
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
		/// Builds a leg sequence for every arm whose contents need to change.
		/// Returns false when there is nothing to animate, having already applied the change.
		/// </summary>
		private bool BeginTransition()
		{
			transitions.Clear();
			Collect(leftArm);
			Collect(rightArm);

			if (transitions.Count == 0)
			{
				return false;
			}

			if (ik == null)
			{
				foreach (ArmTransition transition in transitions)
				{
					ApplyArm(transition.Arm);
				}
				UpdateSheathedFlag();
				return false;
			}

			// Reserve resting places up front, so the hand has somewhere definite to reach for.
			if (sheathe != null)
			{
				foreach (ArmTransition transition in transitions)
				{
					if (transition.Stowing != null)
					{
						sheathe.TryAssign(transition.Stowing, transition.Arm.Side);
					}
				}
			}

			foreach (ArmTransition transition in transitions)
			{
				BeginLeg(transition, transition.Stowing != null ? TransitionLeg.ToStow : TransitionLeg.ToDraw);
			}

			phase = TransitionPhase.Running;
			return true;

			void Collect(ArmState arm)
			{
				RuntimeEquipedData target = Intended(arm);
				if (target == arm.Wielded)
				{
					return;
				}

				transitions.Add(new ArmTransition
				{
					Arm = arm,
					Stowing = arm.Wielded,
					Drawing = target
				});
			}
		}

		/// <summary>
		/// Advances every arm's leg. Each leg's length is the distance the hand actually covers, so a
		/// swap between two armaments sharing a resting place costs almost nothing.
		/// </summary>
		private void RunLegs(float delta)
		{
			bool allDone = true;
			bool allApplied = true;

			foreach (ArmTransition transition in transitions)
			{
				if (transition.Leg != TransitionLeg.Done)
				{
					AdvanceLeg(transition, delta);
					allDone = false;
				}
				if (transition.Leg != TransitionLeg.Recover && transition.Leg != TransitionLeg.Done)
				{
					allApplied = false;
				}
			}

			if (allDone)
			{
				State = PerformanceState.Completed;
			}
			else
			{
				// Finishing once the hands are only travelling home — lets the Actor take a new act.
				State = allApplied ? PerformanceState.Finishing : PerformanceState.Performing;
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

			(Vector3 pos, Quaternion rot) from = LegAnchor(transition, transition.From);
			(Vector3 pos, Quaternion rot) to = LegAnchor(transition, transition.To);

			Vector3 position = Vector3.Lerp(from.pos, to.pos, eased);
			Quaternion rotation = Quaternion.Slerp(from.rot, to.rot, eased);
			float weight = Mathf.Lerp(transition.WeightFrom, transition.WeightTo, eased);

			ik.AddInfluencer(this, transition.Arm.IKChain, ikPriority, position, weight, rotation, weight);

			if (progress >= 1f)
			{
				FinishLeg(transition, position, rotation);
			}
		}

		/// <summary>Resolves a leg endpoint: an armament's resting place, or the frozen hand-over point.</summary>
		private (Vector3 pos, Quaternion rot) LegAnchor(ArmTransition transition, RuntimeEquipedData subject)
		{
			if (subject != null)
			{
				return GetSheathingOrientation(transition.Arm, subject);
			}

			// Frozen in agent space so it tracks the body without feeding back off the hand it drives.
			Transform agentTransform = Agent.Transform;
			return (agentTransform.TransformPoint(transition.FrozenPosition),
				agentTransform.rotation * transition.FrozenRotation);
		}

		private void FinishLeg(ArmTransition transition, Vector3 position, Quaternion rotation)
		{
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

					Freeze(transition, position, rotation);
					BeginLeg(transition, TransitionLeg.Recover);
					break;

				case TransitionLeg.Recover:
					ik.RemoveInfluencer(this, transition.Arm.IKChain);
					transition.Leg = TransitionLeg.Done;
					break;
			}
		}

		private void BeginLeg(ArmTransition transition, TransitionLeg leg)
		{
			transition.Leg = leg;
			transition.Time = 0f;
			Transform hand = transition.Arm.IsLeft ? LeftHand : RightHand;

			switch (leg)
			{
				case TransitionLeg.ToStow:
					// Reach out from wherever the animation has the hand.
					transition.From = transition.Stowing;
					transition.To = transition.Stowing;
					transition.WeightFrom = 0f;
					transition.WeightTo = 1f;
					transition.Duration = DurationFor(Vector3.Distance(hand.position, LegAnchor(transition, transition.Stowing).pos));
					transition.ReachDuration = transition.Duration;
					break;

				case TransitionLeg.ToDraw:
					if (transition.Drawing == null)
					{
						// Nothing to pick up — the stow already happened, so head home.
						(Vector3 pos, Quaternion rot) held = LegAnchor(transition, transition.Stowing);
						StowInactive(transition.Arm);
						UpdateSheathedFlag();
						Freeze(transition, held.pos, held.rot);
						BeginLeg(transition, TransitionLeg.Recover);
						return;
					}

					if (transition.Stowing != null)
					{
						// Carry on from the resting place we just left the old armament at.
						transition.From = transition.Stowing;
						transition.To = transition.Drawing;
						transition.WeightFrom = 1f;
						transition.WeightTo = 1f;
						transition.Duration = DurationFor(Vector3.Distance(
							LegAnchor(transition, transition.Stowing).pos,
							LegAnchor(transition, transition.Drawing).pos));
						transition.ReachDuration = Mathf.Max(transition.ReachDuration, transition.Duration);
					}
					else
					{
						transition.From = transition.Drawing;
						transition.To = transition.Drawing;
						transition.WeightFrom = 0f;
						transition.WeightTo = 1f;
						transition.Duration = DurationFor(Vector3.Distance(hand.position, LegAnchor(transition, transition.Drawing).pos));
						transition.ReachDuration = transition.Duration;
					}
					break;

				case TransitionLeg.Recover:
					// Home again along the span we came out on.
					transition.From = null;
					transition.To = null;
					transition.WeightFrom = 1f;
					transition.WeightTo = 0f;
					transition.Duration = Mathf.Max(transition.ReachDuration, minLegDuration);
					break;
			}
		}

		/// <summary>Pins the hand-over point in agent space, so recovery has somewhere stable to fade from.</summary>
		private void Freeze(ArmTransition transition, Vector3 position, Quaternion rotation)
		{
			Transform agentTransform = Agent.Transform;
			transition.FrozenPosition = agentTransform.InverseTransformPoint(position);
			transition.FrozenRotation = Quaternion.Inverse(agentTransform.rotation) * rotation;
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
			pendingSwapArm = null;
			phase = TransitionPhase.None;
			decideTime = 0f;
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

			if (!SupportsAct(act.Title) ||
				(State != PerformanceState.Inactive && State != PerformanceState.Completed))
			{
				return false;
			}

			bool swap = act.Title != ActorActs.SHEATHE;
			if (!swap && !NeedsTransition())
			{
				return false;
			}

			Act = act;
			RunTime = 0f;
			decideTime = 0f;
			Canceled = false;
			CancelTime = 0f;
			State = PerformanceState.Preparing;

			if (swap)
			{
				// Hold time decides cycle-vs-unarm, so nothing moves until the button resolves.
				pendingSwapArm = GetArm(act.Title == ActorActs.SWAP_LEFT);
				phase = TransitionPhase.Deciding;
			}
			else if (!BeginTransition())
			{
				State = PerformanceState.Inactive;
				Act = null;
				return false;
			}

			AddControlModifier();

			performer = this;
			StartedPreparingEvent?.Invoke(this);
			return true;
		}

		/// <inheritdoc/>
		public bool TryPerform()
		{
			if (phase == TransitionPhase.Deciding)
			{
				// Released before the hold threshold: move to the next armament.
				CommitSwap(false);
				return true;
			}

			return State == PerformanceState.Preparing || State == PerformanceState.Performing;
		}

		/// <inheritdoc/>
		public bool TryCancel(bool force = false)
		{
			if (phase == TransitionPhase.None || !force)
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
		/// Resolves the pending swap and starts moving, or finishes right away when nothing has to move
		/// (a swap while everything is stowed only changes which armament is next out).
		/// </summary>
		private void CommitSwap(bool unarm)
		{
			ArmState arm = pendingSwapArm;
			pendingSwapArm = null;

			if (arm != null)
			{
				if (unarm)
				{
					UnarmArm(arm);
				}
				else
				{
					CycleArm(arm);
				}
			}

			if (BeginTransition())
			{
				StartedPerformingEvent?.Invoke(this);
				return;
			}

			// Nothing had to move. Park in Completing so OnUpdate's tail releases the Actor —
			// leaving phase at None would drop us into Reconcile and never fire the completion.
			phase = TransitionPhase.Completing;
			State = PerformanceState.Completed;
		}

		private void AddControlModifier()
		{
			if (transitionControl < 1f && rigidbodyWrapper != null && controlMod == null)
			{
				controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, transitionControl);
				rigidbodyWrapper.Control.AddModifier(this, controlMod);
			}
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

		/// <summary>
		/// Where the hand must be for <paramref name="subject"/>'s grip to meet its resting place.
		/// </summary>
		private (Vector3 pos, Quaternion rot) GetSheathingOrientation(ArmState arm, RuntimeEquipedData subject)
		{
			// Where the armament's root rests.
			Vector3 slotPos;
			Quaternion slotRot;
			if (sheathe == null || !sheathe.TryGetSlotOrientation(subject, out slotPos, out slotRot))
			{
				Transform fallback = arm.IsLeft ? LeftSheathe : RightSheathe;
				slotPos = fallback.position;
				slotRot = fallback.rotation;
			}

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

			orientation.rot = anchorRot * orientation.rot;
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
			WeaponComponent weapon = data == null ? null : data.Weapon;
			return weapon == null ? null : weapon.MainHand;
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

		/// <summary>Rests an armament at its sheathe point, or on the arm's plain sheathe transform.</summary>
		private void Stow(ArmState arm, RuntimeEquipedData data)
		{
			if (data == null || data.EquipedInstance == null)
			{
				return;
			}

			if (sheathe != null)
			{
				sheathe.TryAssign(data, arm.Side);
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
			transform.SetParent(arm.IsLeft ? LeftHand : RightHand);
			AlignGrip(transform, GripOf(data), slot.pos, slot.rot);
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

		private enum TransitionPhase { None, Deciding, Running, Completing }

		private enum TransitionLeg { None, ToStow, ToDraw, Recover, Done }

		/// <summary>One arm's journey: put away what it holds, pick up what it should, come home.</summary>
		private class ArmTransition
		{
			public ArmState Arm;
			public RuntimeEquipedData Stowing;
			public RuntimeEquipedData Drawing;

			public TransitionLeg Leg;
			public float Time;
			public float Duration;
			public float WeightFrom;
			public float WeightTo;

			/// <summary>Endpoints of the current leg. Null means the frozen hand-over point.</summary>
			public RuntimeEquipedData From;
			public RuntimeEquipedData To;

			/// <summary>How long the outward reach took — recovery retraces it.</summary>
			public float ReachDuration;

			/// <summary>Hand-over point, in agent space so it follows the body.</summary>
			public Vector3 FrozenPosition;
			public Quaternion FrozenRotation;
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
