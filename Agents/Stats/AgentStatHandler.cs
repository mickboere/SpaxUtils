using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling an agent's grouped stat data.
	/// </summary>
	public class AgentStatHandler : EntityComponentMono
	{
		public const string BODY_BACKUP_ID = "BODY_BACKUP";

		// All data accessors self-initialize; stat data is valid regardless of component Awake order.
		public StatOctad BodyLevels { get { EnsureInitialized(); return _bodyLevels; } private set { _bodyLevels = value; } }
		public StatOctad BodyExperience { get { EnsureInitialized(); return _bodyExperience; } private set { _bodyExperience = value; } }
		public StatOctad Physics { get { EnsureInitialized(); return _physics; } private set { _physics = value; } }
		public Vector8 BodyDistribution { get { EnsureInitialized(); return _bodyDistribution; } private set { _bodyDistribution = value; } }
		public PointStatOctad PointStats { get { EnsureInitialized(); return pointStatOctad; } }

		public StatOctad SoulLevels { get { EnsureInitialized(); return _soulLevels; } private set { _soulLevels = value; } }
		public StatOctad SoulExperience { get { EnsureInitialized(); return _soulExperience; } private set { _soulExperience = value; } }
		public Vector8 SoulDistribution { get { EnsureInitialized(); return _soulDistribution; } private set { _soulDistribution = value; } }

		[Header("BODY")]
		[SerializeField] private StatOctadAsset bodyLevels;
		[SerializeField] private StatOctadAsset bodyExperience;
		[SerializeField] private StatOctadAsset physicsOctad;
		[SerializeField] private StatMap bodyAttributeMap;
		[SerializeField] private PointStatOctad pointStatOctad; // Locally defined.
		[Header("SOUL")]
		[SerializeField] private StatOctadAsset soulLevels;
		[SerializeField] private StatOctadAsset soulExperience;
		[SerializeField] private StatMap soulAttributeMap;

		private IAgent agent;

		private StatOctad _bodyLevels;
		private StatOctad _bodyExperience;
		private StatOctad _physics;
		private Vector8 _bodyDistribution;
		private StatOctad _soulLevels;
		private StatOctad _soulExperience;
		private Vector8 _soulDistribution;
		private bool initialized;

		private EntityStat recoveryStat;
		private FloatOperationModifier recoveryMod;

		public void InjectDependencies(IAgent agent,
			[Optional, BindingIdentifier(AgentStatIdentifiers.DISTRIBUTION)] Vector8 generalDistribution,
			[Optional, BindingIdentifier(AgentStatIdentifiers.BODY_DISTRIBUTION)] Vector8 bodyDistribution,
			[Optional, BindingIdentifier(AgentStatIdentifiers.SOUL_DISTRIBUTION)] Vector8 soulDistribution)
		{
			this.agent = agent;
			BodyDistribution = bodyDistribution == Vector8.Zero ? generalDistribution : bodyDistribution;
			SoulDistribution = soulDistribution == Vector8.Zero ? generalDistribution : soulDistribution;
		}

		protected void Awake()
		{
			EnsureInitialized();
			agent.RecoverEvent += RecoverAll;
		}

		/// <summary>
		/// Initializes the stat data if it hasn't been already. Safe to call any number of times.
		/// Lets consumers pull valid stats before this component's own Awake has run.
		/// </summary>
		public void EnsureInitialized()
		{
			// Bail without latching when dependencies aren't injected yet, so a later call still initializes.
			if (initialized || agent == null)
			{
				return;
			}

			// Latch before initializing; InitializeStats reads these same accessors.
			initialized = true;
			InitializeStats();
		}

		private void InitializeStats()
		{
			BodyLevels = bodyLevels.Initialize(agent);
			BodyExperience = bodyExperience.Initialize(agent);
			SoulLevels = soulLevels.Initialize(agent);
			SoulExperience = soulExperience.Initialize(agent);

			// --- BODY INITIALIZATION ---
			bool bodyRanked = false;
			if (BodyDistribution != Vector8.Zero &&
				agent.Stats.TryGetStat(AgentStatIdentifiers.BODY_RANK, out EntityStat bodyRank) &&
				bodyRank.BaseValue > 0f)
			{
				ApplyBudgetDistribution(bodyRank.BaseValue, BodyDistribution, BodyExperience, BodyLevels, bodyAttributeMap);
				bodyRank.BaseValue = 0f;
				bodyRanked = true;
			}

			// --- SOUL INITIALIZATION ---
			bool soulRanked = false;
			if (SoulDistribution != Vector8.Zero &&
				agent.Stats.TryGetStat(AgentStatIdentifiers.SOUL_RANK, out EntityStat soulRank) &&
				soulRank.BaseValue > 0f)
			{
				ApplyBudgetDistribution(soulRank.BaseValue, SoulDistribution, SoulExperience, SoulLevels, soulAttributeMap);
				soulRank.BaseValue = 0f;
				soulRanked = true;
			}

			pointStatOctad.Initialize(agent);
			Physics = physicsOctad.Initialize(agent);

			// Recompute from the initialized levels ONLY for a pool that was actually rank-shaped — otherwise flat base
			// levels would NormalizeMax to a meaningless uniform vector and wipe the injected general distribution.
			if (bodyRanked)
			{
				BodyDistribution = BodyLevels.Vector8.NormalizeMax();
			}
			if (soulRanked)
			{
				SoulDistribution = SoulLevels.Vector8.NormalizeMax();
			}

			// Modify recovery stat with control (so that recovery only occurs when agent is in control).
			if (agent.Body.HasRigidbody && agent.Stats.TryGetStat(AgentStatIdentifiers.RECOVERY, out recoveryStat))
			{
				recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				recoveryStat.AddModifier(this, recoveryMod);
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

		#region Body Backup

		/// <summary>
		/// Stores current body experience in a backup to prevent loss on death.
		/// </summary>
		public void CreateBackup()
		{
			RuntimeDataCollection backup = new RuntimeDataCollection(BODY_BACKUP_ID);
			foreach (EntityStat expStat in BodyExperience.Stats)
			{
				backup.SetValue(expStat.Identifier, expStat.BaseValue);
			}
			agent.RuntimeData.TryAdd(backup, true);
		}

		/// <summary>
		/// Will try to restore body experience from backup, returning true if successful and false if no backup was found.
		/// </summary>
		/// <param name="lost">A new data collection containing the amount of experience points lost per element.</param>
		public void ResetToBackup(out RuntimeDataCollection lost)
		{
			lost = new RuntimeDataCollection("LOST"); // ID will need to be overridden.

			if (!agent.RuntimeData.TryGetEntry(BODY_BACKUP_ID, out RuntimeDataCollection backup))
			{
				// No backup found, simply reset stats.
				foreach (EntityStat expStat in BodyExperience.Stats)
				{
					expStat.BaseValue = 0f;
				}
			}
			else
			{
				// Reset stats to what they were in backup.
				foreach (EntityStat expStat in BodyExperience.Stats)
				{
					float backupValue = backup.GetValue<float>(expStat.Identifier);
					lost.SetValue(expStat.Identifier, expStat.BaseValue - backupValue);
					expStat.BaseValue = backupValue;
				}
			}
		}

		#endregion Body Backup

		/// <summary>
		/// Calculates the total EXP budget for the given Rank, then distributes it according to weights.
		/// This prevents min-maxing from creating astronomically high-level attributes compared to balanced builds.
		/// </summary>
		private void ApplyBudgetDistribution(float rank, Vector8 distribution, StatOctad experience, StatOctad levels, StatMap map)
		{
			// 1. Calculate Total Budget
			// We sum the XP required for EACH stat to reach 'Rank'.
			// This accounts for different curves per attribute if they exist, or if the "Average Rank" implies an average EXP cost.
			float totalExpBudget = 0f;

			for (int i = 0; i < 8; i++)
			{
				string expID = experience[i].Identifier;
				string lvlID = levels[i].Identifier;

				if (map.TryGetMapping(expID, lvlID, out StatMapping mapping))
				{
					// Convert Rank (Level) -> Required EXP for this specific slot
					totalExpBudget += mapping.GetInverseModifierValue(rank);
				}
				else
				{
					SpaxDebug.Warning($"MISSING MAPPING", $"Could not find Leveling mapping for {expID} -> {lvlID}", context: this);
				}
			}

			// 2. Distribute Budget
			// Normalize distribution weights so they sum to 1.
			// NOTE: This now uses ratio-correct allocation so that the resulting Levels preserve the same ratios as the input distribution,
			// while keeping the total budget the same and only normalizing if sum > 1 (sum <= 1 acts as "coverage").
			Vector8 allocated = SpaxFormulas.AllocatePointsForLevelRatios(distribution, totalExpBudget);

			for (int i = 0; i < 8; i++)
			{
				float allocatedExp = allocated[i];

				// Set the Base EXP. The Stat System will automatically recalculate the Level
				// based on the mapping when the stat is next accessed/updated.
				// We use .Max() to ensure we don't accidentally lower EXP if it was already set elsewhere (unlikely during Init, but safe).
				experience[i].BaseValue = experience[i].BaseValue.Max(allocatedExp);
			}
		}
	}
}
