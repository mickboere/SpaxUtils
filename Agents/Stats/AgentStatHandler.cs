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
		public StatOctad Physics { get; private set; }

		[Header("BODY")]
		[SerializeField] private StatOctadAsset bodyLevels;
		[SerializeField] private StatOctadAsset bodyExperience;
		[SerializeField] private StatMap bodyAttributeMap;
		[SerializeField] private PointStatOctad pointStatOctad; // Locally defined.
		[SerializeField] private StatOctadAsset physicsOctad;
		[Header("SOUL")]
		[SerializeField] private StatOctadAsset soulLevels;
		[SerializeField] private StatOctadAsset soulExperience;
		[SerializeField] private StatMap soulAttributeMap;

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
			BodyLevels = bodyLevels.Initialize(agent);
			BodyExperience = bodyExperience.Initialize(agent);
			SoulLevels = soulLevels.Initialize(agent);
			SoulExperience = soulExperience.Initialize(agent);

			// --- BODY INITIALIZATION ---
			if (bodyDistribution != Vector8.Zero &&
				agent.Stats.TryGetStat(AgentStatIdentifiers.BODY_RANK, out EntityStat bodyRank) &&
				bodyRank.BaseValue > 0f)
			{
				// 1. Capture Input (e.g. 10)
				float rankInput = bodyRank.BaseValue;

				// 2. Distribute (10 * 8 = 80 levels)
				ApplyDistribution(rankInput * 8f, bodyDistribution, BodyExperience, BodyLevels, bodyAttributeMap);

				// 3. HANDOVER: Reset Base to 0.
				// Now the stat is purely driven by modifiers (attributes).
				// If we didn't do this, Rank would be 10 (Base) + 10 (derived) = 20.
				bodyRank.BaseValue = 0f;

				// Optional: If you want to force an immediate update to ensure UI sees the new derived value this frame:
				// bodyRank.GetValue(); 
			}

			// --- SOUL INITIALIZATION ---
			if (soulDistribution != Vector8.Zero &&
				agent.Stats.TryGetStat(AgentStatIdentifiers.SOUL_RANK, out EntityStat soulRank) &&
				soulRank.BaseValue > 0f)
			{
				float rankInput = soulRank.BaseValue;
				ApplyDistribution(rankInput * 8f, soulDistribution, SoulExperience, SoulLevels, soulAttributeMap);

				// Handover
				soulRank.BaseValue = 0f;
			}

			pointStatOctad.Initialize(agent);
			Physics = physicsOctad.Initialize(agent);

			// Modify recovery stat with control (so that recovery only occurs when agent is in control).
			if (agent.Body.HasRigidbody && agent.Stats.TryGetStat(AgentStatIdentifiers.RECOVERY, out recoveryStat))
			{
				recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				recoveryStat.AddModifier(this, recoveryMod);
			}
		}

		private void ApplyDistribution(float level, Vector8 distribution, StatOctad experience, StatOctad levels, StatMap map)
		{
			Vector8 targetLevels = distribution.Normalize() * level;

			for (int i = 0; i < 8; i++)
			{
				string expID = experience[i].Identifier;
				string lvlID = levels[i].Identifier;

				// 1. LOOKUP
				if (map.TryGetMapping(expID, lvlID, out StatMapping mapping))
				{
					// 2. CALCULATE INVERSE
					float targetLvl = (targetLevels[i] - 1f).Round().Max(0f);
					float requiredExp = mapping.GetInverseModifierValue(targetLvl);

					// 3. APPLY
					experience[i].BaseValue = experience[i].BaseValue.Max(requiredExp);
				}
				else
				{
					SpaxDebug.Warning($"MISSING MAPPING", $"Could not find Leveling mapping for {expID} -> {lvlID}", context: this);
				}
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

		/// <summary>
		/// Updates the point stats with <paramref name="delta"/> time.
		/// </summary>
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

		/// <summary>
		/// Will try to return a defined <see cref="PointsStat"/> with ID <paramref name="stat"/>.
		/// </summary>
		/// <param name="stat"></param>
		/// <param name="pointStat"></param>
		/// <returns></returns>
		public bool TryGetPointStat(string stat, out PointsStat pointStat)
		{
			for (int i = 0; i < 8; i++)
			{
				if (pointStatOctad[i].Identifier == stat)
				{
					pointStat = pointStatOctad[i];
					return true;
				}
			}
			pointStat = null;
			return false;
		}
	}
}
