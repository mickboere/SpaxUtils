using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_Guarding", menuName = "ScriptableObjects/Combat/GuardingBehaviourAsset")]
	public class GuardingBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		private EntityStat defenceStat;
		private EntityStat guardStat;

		private FloatFuncModifier guardMod;

		public void InjectDependencies()
		{
			defenceStat = Agent.GetStat(AgentStatIdentifiers.DEFENCE);
			guardStat = Agent.GetStat(AgentStatIdentifiers.GUARD);
		}

		public override void Start()
		{
			base.Start();

			guardMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + guardStat * Weight);
			defenceStat.AddModifier(this, guardMod);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

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
