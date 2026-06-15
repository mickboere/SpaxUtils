using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that manages guard state, writing the guard weight to agent runtime data each frame
	/// and setting up the perfect-block window for incoming hits.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(GuardingCombatBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(GuardingCombatBehaviourAsset))]
	public class GuardingCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		protected bool InWindow => !enablePerfectBlock ? false :
			Performer.ChargeTime > Move.MinCharge * windowShift &&
			Performer.ChargeTime < Move.MinCharge * windowShift + blockWindow;

		[SerializeField] private bool enablePerfectBlock;
		[SerializeField, Range(0f, 1f), Tooltip("The perfect-block time window which negates all damages.")] private float blockWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at ending of minimum charge.")] private float windowShift = 1f;

		[SerializeField, Tooltip("When enabled, committing to guard turns the agent to face its current target. The forward travels from the facing at guard-start toward the target in step with the guard weight, so it has fully turned by the time guard is at full weight.")]
		private bool faceTargetOnGuard = true;

		private IHittable hittable;
		private ITargeter targeter;
		private IAgentMovementHandler movementHandler;

		private PointsStat chargeStat;
		private EntityStat vulnerabilityStat;
		private FloatFuncModifier vulnerabilityMod;
		private Vector3 entryForward;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable,
			ITargeter targeter, IAgentMovementHandler movementHandler)
		{
			this.hittable = hittable;
			this.targeter = targeter;
			this.movementHandler = movementHandler;

			agentStatHandler.TryGetPointStat(Move.ChargeCost.Stat, out chargeStat);
		}

		public override void Start()
		{
			base.Start();

			// Guard sacrifices mobility for focused invulnerability: it drives Vulnerability (and thus crit
			// chance) toward 0 as guard weight rises, making crits impossible at full guard. The rear stays
			// exposed - that exposure is applied situationally per-hit in AgentHitHandlerComponent, lerping
			// back up from this guard-reduced value, so a guarding agent can still be critted from behind.
			vulnerabilityStat = Agent.Stats.GetStat(AgentStatIdentifiers.VULNERABILITY, true);
			vulnerabilityMod = new FloatFuncModifier(ModMethod.Absolute, (v) => v * Weight.Invert());
			vulnerabilityStat.AddModifier(this, vulnerabilityMod);

			// Capture the facing at guard-start so the turn-to-target travels from here in step with guard weight.
			entryForward = RigidbodyWrapper.Forward.FlattenY().normalized;

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			Agent.RuntimeData.SetValue(AgentDataIdentifiers.GUARD_WEIGHT, 0f, dirty: false);

			vulnerabilityStat.RemoveModifier(this);
			vulnerabilityMod.Dispose();

			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Agent.RuntimeData.SetValue(AgentDataIdentifiers.GUARD_WEIGHT, Weight, dirty: false);

			// Turn to face the target as guard commits: slerp the forward from the guard-start facing toward
			// the target by the guard weight, so it arrives on-target as guard reaches full weight (and keeps
			// tracking afterward). The base control modifier zeroes movement Control with weight, so auto-rotation
			// is already suppressed here and won't fight this.
			if (faceTargetOnGuard && targeter.Target != null)
			{
				Vector3 toTarget = (targeter.Target.Position - RigidbodyWrapper.Position).FlattenY();
				if (toTarget.sqrMagnitude > 0.0001f)
				{
					movementHandler.ForceRotation(entryForward.Slerp(toTarget.normalized, Weight));
				}
			}

			if (Performer.State == PerformanceState.Preparing && chargeStat != null)
			{
				// Drain charge stat.
				chargeStat.Drain(Move.ChargeCost.Cost * delta, out bool drained);
				if (Move.ChargeCost.Required && drained)
				{
					Performer.TryPerform();
				}
			}
		}

		private void OnHitEvent(HitData hitData)
		{
			// Hit by enemy attack during guard.
			hitData.Data.SetValue(HitDataIdentifiers.GUARD_WEIGHT, Weight);

			if (InWindow)
			{
				hitData.Data.SetValue(HitDataIdentifiers.BLOCKED, true);
			}
		}
	}
}
