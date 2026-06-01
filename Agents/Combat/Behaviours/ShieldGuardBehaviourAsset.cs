using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Equipment behaviour for shields. While the agent is guarding, maps the shield's defensive
	/// physics (W/E/SE/SW) onto the corresponding body stats, scaled by the body GUARD stat and
	/// the current guard weight. Modifiers are always registered and evaluate to zero when not guarding.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(ShieldGuardBehaviourAsset), menuName = "Performance/Behaviour/" + nameof(ShieldGuardBehaviourAsset))]
	public class ShieldGuardBehaviourAsset : BehaviourAsset
	{
		private IAgent agent;
		private RuntimeItemData runtimeItemData;
		private AgentStatHandler agentStatHandler;

		private float physic_W;
		private float physic_E;
		private float physic_SE;
		private float physic_SW;

		private EntityStat guardStat;
		private EntityStat proofingStat;
		private EntityStat pliancyStat;
		private EntityStat protectionStat;
		private EntityStat preservationStat;

		private FloatFuncModifier proofingMod;
		private FloatFuncModifier pliancyMod;
		private FloatFuncModifier protectionMod;
		private FloatFuncModifier preservationMod;

		public void InjectDependencies(IAgent agent, RuntimeItemData runtimeItemData, AgentStatHandler agentStatHandler)
		{
			this.agent = agent;
			this.runtimeItemData = runtimeItemData;
			this.agentStatHandler = agentStatHandler;
		}

		public override void Start()
		{
			base.Start();

			CachePhysics();
			runtimeItemData.RuntimeData.DataUpdatedEvent += OnDataUpdated;

			guardStat = agent.Stats.GetStat(AgentStatIdentifiers.GUARD, true);
			proofingStat = agent.Stats.GetStat(AgentStatIdentifiers.PROOFING, true);
			pliancyStat = agent.Stats.GetStat(AgentStatIdentifiers.PLIANCY, true);
			protectionStat = agent.Stats.GetStat(AgentStatIdentifiers.PROTECTION, true);
			preservationStat = agent.Stats.GetStat(AgentStatIdentifiers.PRESERVATION, true);

			float GetWeight() => agent.RuntimeData.GetValue<float>(AgentDataIdentifiers.GUARD_WEIGHT, 0f);

			proofingMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_W * guardStat.Value * GetWeight());
			pliancyMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_E * guardStat.Value * GetWeight());
			protectionMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_SE * guardStat.Value * GetWeight());
			preservationMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_SW * guardStat.Value * GetWeight());

			proofingStat.AddModifier(this, proofingMod);
			pliancyStat.AddModifier(this, pliancyMod);
			protectionStat.AddModifier(this, protectionMod);
			preservationStat.AddModifier(this, preservationMod);
		}

		public override void Stop()
		{
			base.Stop();

			runtimeItemData.RuntimeData.DataUpdatedEvent -= OnDataUpdated;

			proofingStat.RemoveModifier(this);
			pliancyStat.RemoveModifier(this);
			protectionStat.RemoveModifier(this);
			preservationStat.RemoveModifier(this);

			proofingMod.Dispose();
			pliancyMod.Dispose();
			protectionMod.Dispose();
			preservationMod.Dispose();
		}

		private void CachePhysics()
		{
			physic_W = runtimeItemData.RuntimeData.GetValue<float>(agentStatHandler.Physics.GetIdentifier(6), 0f);
			physic_E = runtimeItemData.RuntimeData.GetValue<float>(agentStatHandler.Physics.GetIdentifier(2), 0f);
			physic_SE = runtimeItemData.RuntimeData.GetValue<float>(agentStatHandler.Physics.GetIdentifier(3), 0f);
			physic_SW = runtimeItemData.RuntimeData.GetValue<float>(agentStatHandler.Physics.GetIdentifier(5), 0f);
		}

		private void OnDataUpdated(RuntimeDataEntry entry)
		{
			if (!Running || entry == null)
			{
				return;
			}

			if (entry.ID == ItemDataIdentifiers.RANK || entry.ID == ItemDataIdentifiers.QUALITY)
			{
				CachePhysics();
			}
		}
	}
}
