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
		private EntityStat armorStat;
		private EntityStat yieldStat;
		private EntityStat wardStat;
		private EntityStat comfortStat;

		private FloatFuncModifier armorMod;
		private FloatFuncModifier yieldMod;
		private FloatFuncModifier wardMod;
		private FloatFuncModifier comfortMod;

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
			armorStat = agent.Stats.GetStat(AgentStatIdentifiers.ARMOR, true);
			yieldStat = agent.Stats.GetStat(AgentStatIdentifiers.YIELD, true);
			wardStat = agent.Stats.GetStat(AgentStatIdentifiers.WARD, true);
			comfortStat = agent.Stats.GetStat(AgentStatIdentifiers.COMFORT, true);

			float GetWeight() => agent.RuntimeData.GetValue<float>(AgentDataIdentifiers.GUARD_WEIGHT, 0f);

			armorMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_W * guardStat.Value * GetWeight());
			yieldMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_E * guardStat.Value * GetWeight());
			wardMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_SE * guardStat.Value * GetWeight());
			comfortMod = new FloatFuncModifier(ModMethod.Additive, (v) => v + physic_SW * guardStat.Value * GetWeight());

			armorStat.AddModifier(this, armorMod);
			yieldStat.AddModifier(this, yieldMod);
			wardStat.AddModifier(this, wardMod);
			comfortStat.AddModifier(this, comfortMod);
		}

		public override void Stop()
		{
			base.Stop();

			runtimeItemData.RuntimeData.DataUpdatedEvent -= OnDataUpdated;

			armorStat.RemoveModifier(this);
			yieldStat.RemoveModifier(this);
			wardStat.RemoveModifier(this);
			comfortStat.RemoveModifier(this);

			armorMod.Dispose();
			yieldMod.Dispose();
			wardMod.Dispose();
			comfortMod.Dispose();
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
