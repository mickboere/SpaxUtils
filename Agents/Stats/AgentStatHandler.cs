using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling an agent's grouped stat data.
	/// </summary>
	public class AgentStatHandler : EntityComponentMono
	{
		public StatOctad BodyLevels { get; private set; }
		public StatOctad BodyExperience { get; private set; }
		public StatOctad SoulLevels { get; private set; }
		public StatOctad SoulExperience { get; private set; }
		public PointStatOctad PointStats => pointStatOctad;

		[SerializeField] private StatOctadAsset bodyLevels;
		[SerializeField] private StatOctadAsset bodyExperience;
		[SerializeField] private StatMap bodyAttributeMap;
		[SerializeField] private StatOctadAsset soulLevels;
		[SerializeField] private StatOctadAsset soulExperience;
		[SerializeField] private StatMap soulAttributeMap;
		[SerializeField] private PointStatOctad pointStatOctad;

		private IAgent agent;
		private Vector8 bodyDistribution;
		private Vector8 soulDistribution;

		private EntityStat recoveryStat;
		private FloatOperationModifier recoveryMod;

		public void InjectDependencies(IAgent agent,
			[Optional, BindingIdentifier(AgentStatIdentifiers.BODY_DISTRIBUTION)] Vector8 bodyDistribution,
			[Optional, BindingIdentifier(AgentStatIdentifiers.SOUL_DISTRIBUTION)] Vector8 soulDistribution)
		{
			this.agent = agent;
			this.bodyDistribution = bodyDistribution;
			this.soulDistribution = soulDistribution;
		}

		protected void Awake()
		{
			InitializeStats();
			agent.RecoverEvent += RecoverAll;
		}

		private void InitializeStats()
		{
			BodyLevels = bodyLevels.Get(agent);
			BodyExperience = bodyExperience.Get(agent);
			SoulLevels = soulLevels.Get(agent);
			SoulExperience = soulExperience.Get(agent);

			// Apply distributions where able.
			if (bodyDistribution != Vector8.Zero && agent.Stats.TryGetStat(AgentStatIdentifiers.BODY_LEVEL, out EntityStat bodyLevel) && bodyLevel.BaseValue > 0f)
			{
				ApplyDistribution(bodyLevel.BaseValue * 8f, bodyDistribution, BodyExperience, bodyAttributeMap);
				bodyLevel.BaseValue = 0f;
				bodyLevel.Data.Dirty = false;
			}
			if (soulDistribution != Vector8.Zero && agent.Stats.TryGetStat(AgentStatIdentifiers.SOUL_LEVEL, out EntityStat soulLevel) && soulLevel.BaseValue > 0f)
			{
				ApplyDistribution(soulLevel.BaseValue * 8f, soulDistribution, SoulExperience, soulAttributeMap);
				soulLevel.BaseValue = 0f;
				soulLevel.Data.Dirty = false;
			}

			pointStatOctad.Initialize(agent);

			// Modify recovery stat with control (so that recovery only occurs when agent is in control).
			if (agent.Body.HasRigidbody && agent.Stats.TryGetStat(AgentStatIdentifiers.RECOVERY, out recoveryStat))
			{
				recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				recoveryStat.AddModifier(this, recoveryMod);
			}
		}

		private void ApplyDistribution(float level, Vector8 distribution, StatOctad experience, StatMap map)
		{
			Vector8 targetLevels = distribution.Normalize() * level;
			for (int i = 0; i < 8; i++)
			{
				experience[i].BaseValue = experience[i].BaseValue.Max(map.FromStatMappings[experience[i].Identifier].GetInverseModifierValue((targetLevels[i] - 1f).Round().Max(0f)));
			}
		}

		protected void OnDestroy()
		{
			// Clean up.
			if (recoveryStat != null)
			{
				recoveryStat.RemoveModifier(this);
			}
			agent.RecoverEvent -= RecoverAll;
		}

		public void UpdateStats(float delta)
		{
			if (recoveryMod != null)
			{
				recoveryMod.SetValue(agent.Body.RigidbodyWrapper.Control);
			}

			pointStatOctad.Update(delta);
		}

		/// <summary>
		/// Recovers all point stats.
		/// </summary>
		public void RecoverAll()
		{
			pointStatOctad.Recover();
		}
	}
}
