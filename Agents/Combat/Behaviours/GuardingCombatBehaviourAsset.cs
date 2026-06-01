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
		protected bool InWindow =>
			Performer.ChargeTime > Move.MinCharge * windowShift &&
			Performer.ChargeTime < Move.MinCharge * windowShift + blockWindow;

		[SerializeField, Range(0f, 1f), Tooltip("The perfect-block time window which negates all damages.")] private float blockWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at ending of minimum charge.")] private float windowShift = 1f;

		private IHittable hittable;

		private PointsStat chargeStat;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable)
		{
			this.hittable = hittable;

			agentStatHandler.TryGetPointStat(Move.ChargeCost.Stat, out chargeStat);
		}

		public override void Start()
		{
			base.Start();

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			Agent.RuntimeData.SetValue(AgentDataIdentifiers.GUARD_WEIGHT, 0f, dirty: false);

			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Agent.RuntimeData.SetValue(AgentDataIdentifiers.GUARD_WEIGHT, Weight, dirty: false);

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
