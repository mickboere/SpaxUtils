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

			defenceMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + guardStat * Weight);
			defenceStat.AddModifier(this, defenceMod);

			enduranceDamageMod = new FloatFuncModifier(ModMethod.Absolute, (damage) => damage * (guardStat * agentStatHandler.PointStatOctad.W.PercentileMax * Weight).InvertClamped());
			agentStatHandler.PointStatOctad.W.Cost.AddModifier(this, enduranceDamageMod);

			hittable.Subscribe(this, OnHitEvent, 1000);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
			agentStatHandler.PointStatOctad.W.Cost.RemoveModifier(this);
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
			hitData.Result_Blocked = Weight;
		}
	}
}
