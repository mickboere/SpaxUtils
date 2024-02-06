using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour that adjust the Agent's stats while guarding.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_Guarding", menuName = "ScriptableObjects/Combat/GuardingBehaviourAsset")]
	public class GuardingBehaviourAsset : BasePerformanceMoveBehaviourAsset
	{
		private EntityStat defenceStat;
		private EntityStat guardStat;
		private EntityStat recoveryStat;

		private FloatFuncModifier guardMod;
		private FloatOperationModifier recoveryMod;

		public void InjectDependencies()
		{
			defenceStat = Agent.GetStat(AgentStatIdentifiers.DEFENCE);
			guardStat = Agent.GetStat(AgentStatIdentifiers.GUARD);
			recoveryStat = Agent.GetStat(AgentStatIdentifiers.RECOVERY);
		}

		public override void Start()
		{
			base.Start();

			guardMod = new FloatFuncModifier(ModMethod.Additive, (defence) => defence + guardStat * Weight);
			recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 0f);

			defenceStat.AddModifier(this, guardMod);
			recoveryStat.AddModifier(this, recoveryMod);
		}

		public override void Stop()
		{
			base.Stop();

			defenceStat.RemoveModifier(this);
			recoveryStat.RemoveModifier(this);

			guardMod.Dispose();
			recoveryMod.Dispose();
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

			guardMod.FuncChanged();
		}
	}
}
