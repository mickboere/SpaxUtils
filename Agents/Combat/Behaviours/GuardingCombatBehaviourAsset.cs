using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(GuardingCombatBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(GuardingCombatBehaviourAsset))]
	public class GuardingCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		protected bool InWindow =>
			Performer.ChargeTime > Move.MinCharge * windowShift &&
			Performer.ChargeTime < Move.MinCharge * windowShift + blockWindow;

		[SerializeField, Range(0f, 1f), Tooltip("The perfect-block time window which negates all damages.")] private float blockWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at ending of minimum charge.")] private float windowShift = 1f;

		private AgentStatHandler agentStatHandler;
		private IHittable hittable;

		private PointsStat chargeStat;
		private EntityStat defenceStat;
		private EntityStat guardStat;

		private FloatFuncModifier defenceMod;
		private FloatFuncModifier enduranceDamageMod;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;

			agentStatHandler.TryGetPointStat(Move.ChargeCost.Stat, out chargeStat);
			defenceStat = Agent.Stats.GetStat(AgentStatIdentifiers.PLATING);
			guardStat = Agent.Stats.GetStat(AgentStatIdentifiers.GUARD);
		}

		public override void Start()
		{
			base.Start();

			defenceMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + defence * guardStat * Weight);
			defenceStat.AddModifier(this, defenceMod);

			enduranceDamageMod = new FloatFuncModifier(ModMethod.Absolute, (damage) => damage * (InWindow ? 0f : (1f / (guardStat * Weight).Max(1f))));
			agentStatHandler.PointStats.W.DrainMult.AddModifier(this, enduranceDamageMod);

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
			agentStatHandler.PointStats.W.DrainMult.RemoveModifier(this);

			defenceMod.Dispose();
			enduranceDamageMod.Dispose();

			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

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
			hitData.Data.SetValue(HitDataIdentifiers.GUARD, Weight);

			if (InWindow)
			{
				hitData.Data.SetValue(HitDataIdentifiers.BLOCKED, true);
			}
		}
	}
}
