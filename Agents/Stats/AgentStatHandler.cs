using System;
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
		private ExpSettings expSettings;

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

		private EntityStat[] bodyExpGain;
		private EntityStat[] soulExpGain;
		private Action<float>[] reserveGainedHandlers;
		private Dictionary<string, float> expDecay;
		private List<string> expDecaying;

		public void InjectDependencies(IAgent agent, ExpSettings expSettings,
			[Optional, BindingIdentifier(AgentStatIdentifiers.DISTRIBUTION)] Vector8 generalDistribution,
			[Optional, BindingIdentifier(AgentStatIdentifiers.BODY_DISTRIBUTION)] Vector8 bodyDistribution,
			[Optional, BindingIdentifier(AgentStatIdentifiers.SOUL_DISTRIBUTION)] Vector8 soulDistribution)
		{
			this.agent = agent;
			this.expSettings = expSettings;
			BodyDistribution = bodyDistribution == Vector8.Zero ? generalDistribution : bodyDistribution;
			SoulDistribution = soulDistribution == Vector8.Zero ? generalDistribution : soulDistribution;
		}

		protected void Awake()
		{
			EnsureInitialized();
			agent.RecoverEvent += RecoverAll;
		}

		protected void OnEnable()
		{
			SubscribeExpConditions();
		}

		protected void OnDisable()
		{
			UnsubscribeExpConditions();
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

			// Cache both EXP gain multipliers per element; body level drives the soul's rate and vice versa.
			bodyExpGain = new EntityStat[8];
			soulExpGain = new EntityStat[8];
			for (int i = 0; i < 8; i++)
			{
				bodyExpGain[i] = agent.Stats.GetStat(BodyExperience[i].Identifier.SubStat(AgentStatIdentifiers.SUB_GAIN), true, 1f);
				soulExpGain[i] = agent.Stats.GetStat(SoulExperience[i].Identifier.SubStat(AgentStatIdentifiers.SUB_GAIN), true, 1f);
			}

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
			UpdateExpDecay(delta);
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

		#region Experience

		/// <summary>
		/// Rewards experience for an amount measured in BARS: 1 bar equals a full emptying of <paramref name="element"/>'s
		/// point-stat, which is what makes rewards comparable across the eight elements.
		/// </summary>
		/// <param name="source">Optional <see cref="ExpSources"/> identifier; carries the weight and anti-farm decay.</param>
		/// <param name="bodyShare">Fraction rewarded to the body attribute. Spellwork feeds the soul only.</param>
		/// <param name="soulShare">Fraction rewarded to the soul attribute.</param>
		/// <returns>The experience rewarded, before shares.</returns>
		public float RewardExp(Element element, float bars, string source = null, float bodyShare = 1f, float soulShare = 1f)
		{
			return RewardExp((int)element, bars, source, bodyShare, soulShare);
		}

		/// <inheritdoc cref="RewardExp(Element, float, string, float, float)"/>
		public float RewardExp(int element, float bars, string source = null, float bodyShare = 1f, float soulShare = 1f)
		{
			EnsureInitialized();

			if (bars <= 0f || expSettings == null || bodyExpGain == null)
			{
				return 0f;
			}

			// Only rewarded while under the agent's own control; excludes menus, cutscenes, lobby and spawning.
			if (agent.Brain == null || !agent.Brain.IsStateActive(AgentStateIdentifiers.CONTROL))
			{
				return 0f;
			}

			ExpSettings.Source config = expSettings.GetSource(source);
			float exp = bars * ConsumeDecay(source, config.decayTime, bars) * config.weight *
				expSettings.GetElementWeight(element) * expSettings.Multiplier * PointStats[element].Max;

			if (exp <= 0f)
			{
				return 0f;
			}

			if (bodyShare > 0f)
			{
				BodyExperience[element].BaseValue += exp * bodyShare * bodyExpGain[element];
			}
			if (soulShare > 0f)
			{
				SoulExperience[element].BaseValue += exp * soulShare * soulExpGain[element];
			}

			return exp;
		}

		/// <summary>
		/// <see cref="RewardExp(Element, float, string, float, float)"/> for an amount measured in points of
		/// <paramref name="element"/>'s own point-stat; converts to bars.
		/// </summary>
		public float RewardExpPoints(Element element, float points, string source = null, float bodyShare = 1f, float soulShare = 1f)
		{
			float max = PointStats[(int)element].Max;
			return max > 0f ? RewardExp(element, points / max, source, bodyShare, soulShare) : 0f;
		}

		/// <summary>
		/// Consumes the anti-farm multiplier for <paramref name="source"/> and returns the value to reward at.
		/// A source can never pay more than 1 bar per <paramref name="decayTime"/> seconds.
		/// </summary>
		private float ConsumeDecay(string source, float decayTime, float bars)
		{
			if (string.IsNullOrEmpty(source) || decayTime <= 0f)
			{
				return 1f;
			}

			expDecay ??= new Dictionary<string, float>();
			expDecaying ??= new List<string>();

			if (!expDecay.TryGetValue(source, out float multiplier))
			{
				multiplier = 1f;
			}

			// Paid at the pre-drain multiplier, then drained by the amount itself: a full bar pays full and empties it.
			expDecay[source] = Mathf.Max(0f, multiplier - bars);
			if (!expDecaying.Contains(source))
			{
				expDecaying.Add(source);
			}

			return multiplier;
		}

		private void UpdateExpDecay(float delta)
		{
			if (expDecaying == null)
			{
				return;
			}

			for (int i = expDecaying.Count - 1; i >= 0; i--)
			{
				string source = expDecaying[i];
				float decayTime = expSettings.GetSource(source).decayTime;
				float multiplier = Mathf.Min(1f, expDecay[source] + (decayTime > 0f ? delta / decayTime : 1f));
				expDecay[source] = multiplier;

				if (multiplier >= 1f)
				{
					expDecaying.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Hooks the EXP conditions the point-stats report themselves: overdrawing, recovering and regaining reserve.
		/// Act-shaped conditions (striking, guarding, jumping) reward from their own call sites.
		/// Uses <see cref="pointStatOctad"/> directly; the point-stats exist before initialization, so a subscription
		/// here never forces it.
		/// </summary>
		private void SubscribeExpConditions()
		{
			pointStatOctad.N.OverdrawnEvent += OnEnergyOverdrawn;
			pointStatOctad.SE.RecoveredEvent += OnGraceGained;
			pointStatOctad.SE.DrainedEvent += OnGraceDrained;
			pointStatOctad.S.DrainedEvent += OnManaSpent;
			pointStatOctad.SW.RecoveredEvent += OnHealthRecovered;

			// Any stat's reserve recovery pays Nature; cached per index so they can be unsubscribed again.
			reserveGainedHandlers ??= CreateReserveGainedHandlers();
			for (int i = 0; i < 8; i++)
			{
				pointStatOctad[i].ReserveGainedEvent += reserveGainedHandlers[i];
			}
		}

		private void UnsubscribeExpConditions()
		{
			pointStatOctad.N.OverdrawnEvent -= OnEnergyOverdrawn;
			pointStatOctad.SE.RecoveredEvent -= OnGraceGained;
			pointStatOctad.SE.DrainedEvent -= OnGraceDrained;
			pointStatOctad.S.DrainedEvent -= OnManaSpent;
			pointStatOctad.SW.RecoveredEvent -= OnHealthRecovered;

			if (reserveGainedHandlers != null)
			{
				for (int i = 0; i < 8; i++)
				{
					pointStatOctad[i].ReserveGainedEvent -= reserveGainedHandlers[i];
				}
			}
		}

		private Action<float>[] CreateReserveGainedHandlers()
		{
			Action<float>[] handlers = new Action<float>[8];
			for (int i = 0; i < 8; i++)
			{
				int index = i;
				handlers[i] = (amount) => OnReserveGained(index, amount);
			}
			return handlers;
		}

		private void OnEnergyOverdrawn(float amount) => RewardExpPoints(Element.Fire, amount, ExpSources.ENERGY_OVERDRAW);
		private void OnGraceGained(float amount) => RewardExpPoints(Element.Spirit, amount, ExpSources.GRACE_GAIN);
		private void OnGraceDrained(float amount) => RewardExpPoints(Element.Spirit, amount, ExpSources.GRACE_DRAIN);
		private void OnManaSpent(float amount) => RewardExpPoints(Element.Water, amount, ExpSources.MANA_SPEND);
		private void OnHealthRecovered(float amount) => RewardExpPoints(Element.Nature, amount, ExpSources.HEALTH_RECOVERY);

		private void OnReserveGained(int index, float amount)
		{
			// Measured against the stat that regained it, rewarded to Nature which owns all recovery.
			float max = PointStats[index].Max;
			if (max > 0f)
			{
				RewardExp(Element.Nature, amount / max, ExpSources.RESERVE_RECOVERY);
			}
		}

		#endregion Experience

		#region Death

		/// <summary>
		/// The soul caps off the body: per element, body EXP exceeding soul EXP is stripped and reported in <paramref name="lost"/>.
		/// Elements where the soul leads are untouched.
		/// </summary>
		/// <param name="lost">A new data collection containing the amount of experience points lost per element.</param>
		public void ResetToSoul(out RuntimeDataCollection lost)
		{
			lost = new RuntimeDataCollection("LOST"); // ID will need to be overridden.

			for (int i = 0; i < 8; i++)
			{
				EntityStat bodyExp = BodyExperience[i];
				float soulExp = SoulExperience[i].BaseValue;
				float surplus = Mathf.Max(0f, bodyExp.BaseValue - soulExp);
				lost.SetValue(bodyExp.Identifier, surplus);
				bodyExp.BaseValue -= surplus;
			}
		}

		#endregion Death

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
