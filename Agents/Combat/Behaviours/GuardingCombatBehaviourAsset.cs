using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_Guarding", menuName = "ScriptableObjects/Combat/GuardingCombatBehaviourAsset")]
	public class GuardingCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		protected bool InWindow =>
			Performer.Charge > Move.MinCharge * windowShift &&
			Performer.Charge < Move.MinCharge * windowShift + blockWindow;

		[SerializeField, Range(0f, 1f), Tooltip("The perfect-block time window which negates all damages.")] private float blockWindow = 0.1f;
		[SerializeField, Range(0f, 1f), Tooltip("0 is at beginning of charge, 1 is at ending of minimum charge.")] private float windowShift = 1f;

		private AgentStatHandler agentStatHandler;
		private IHittable hittable;

		private EntityStat defenceStat;
		private EntityStat guardStat;

		private FloatFuncModifier defenceMod;
		private FloatFuncModifier enduranceDamageMod;

		public void InjectDependencies(AgentStatHandler agentStatHandler, IHittable hittable)
		{
			this.agentStatHandler = agentStatHandler;
			this.hittable = hittable;

			defenceStat = Agent.Stats.GetStat(AgentStatIdentifiers.DEFENCE);
			guardStat = Agent.Stats.GetStat(AgentStatIdentifiers.GUARD);
		}

		public override void Start()
		{
			base.Start();

			defenceMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + defence * guardStat * Weight);
			defenceStat.AddModifier(this, defenceMod);

			enduranceDamageMod = new FloatFuncModifier(ModMethod.Absolute, (damage) => damage * (InWindow ? 0f : (1f / (guardStat * Weight).Max(1f))));
			agentStatHandler.PointStats.W.Cost.AddModifier(this, enduranceDamageMod);

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
			agentStatHandler.PointStats.W.Cost.RemoveModifier(this);

			defenceMod.Dispose();
			enduranceDamageMod.Dispose();

			hittable.Unsubscribe(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Performer.State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				if (Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, Move.ChargeCost.Cost * delta, false, out _, out bool drained) && drained)
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
