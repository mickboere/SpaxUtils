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

		private EntityStat defenceStat;
		private EntityStat guardStat;

		private FloatFuncModifier defenceMod;
		private FloatFuncModifier enduranceMod;

		public void InjectDependencies(AgentStatHandler agentStatHandler)
		{
			this.agentStatHandler = agentStatHandler;
			defenceStat = Agent.GetStat(AgentStatIdentifiers.DEFENCE);
			guardStat = Agent.GetStat(AgentStatIdentifiers.GUARD);
		}

		public override void Start()
		{
			base.Start();

			defenceMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + guardStat * Weight);
			defenceStat.AddModifier(this, defenceMod);

			enduranceMod = new FloatFuncModifier(ModMethod.Absolute, (endurance) => endurance / (guardStat * 0.01f).LerpTo(1f, 1f - Weight));
			agentStatHandler.PointStatOcton.W.Cost.AddModifier(this, enduranceMod);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
			agentStatHandler.PointStatOcton.W.Cost.RemoveModifier(this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (Performer.State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				Agent.TryApplyStatCost(Move.ChargeCost, delta, out bool drained);
				if (drained)
				{
					Performer.TryPerform();
				}
			}
		}
	}
}
