using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Exposes live combat information about an <see cref="IAgent"/>.
	/// Also evaluates a personality-biased preferred move + combo chain
	/// against the current target.
	/// </summary>
	public class AgentCombatComponent : AgentComponentBase
	{
		public AgentStatHandler StatHandler { get; private set; }

		/// <summary>True when the agent actively targets or performs combat actions.</summary>
		public bool InCombatMode { get; private set; }

		/// <summary>Whether the agent's stun handler is currently in the stunned state.</summary>
		public bool Stunned => stunHandler.Stunned;

		/// <summary>The combat move that is currently being prepared/performed (if any).</summary>
		public ICombatMove CurrentCombatMove { get; private set; }

		/// <summary>How exposed the agent currently is to being hit (0-1).</summary>
		public float Openness { get; private set; }

		/// <summary>Reach contributed by stats/equipment before the active move is considered.</summary>
		public float BaseReach { get; private set; }

		/// <summary>Total reach including the active combat move.</summary>
		public float ActiveReach => CurrentCombatMove == null ? BaseReach : BaseReach + CurrentCombatMove.Range;

		/// <summary>The current maximum offensive output of this agent.</summary>
		public float Offense { get; private set; }

		/// <summary>The current maximum power output of this agent.</summary>
		public float Power => powerStat.Value;

		/// <summary>The current poise stat value.</summary>
		public float Poise => poiseStat.Value;

		#region Move Selection

		/// <summary>Preferred opening act to send as input.</summary>
		public string PreferredAct { get; private set; }

		/// <summary>Preferred offensive move (typically the final move in the planned chain).</summary>
		public ICombatMove PreferredMove { get; private set; }

		/// <summary>
		/// Estimated effective melee reach of <see cref="PreferredMove"/>:
		/// base reach + move range (+ limb reach for melee), NO storm distance.
		/// </summary>
		public float PreferredMoveReach { get; private set; }

		/// <summary>
		/// Planned input chain (acts) to reach <see cref="PreferredMove"/>.
		/// First entry is the opening act.
		/// </summary>
		public string[] PreferredMoveInputChain { get; private set; } = Array.Empty<string>();

		#endregion Move Selection

		[Header("Reach")]
		[SerializeField] private float fallbackBaseReach = 0.5f;

		[SerializeField]
		[Tooltip("Maximum allowed range error before range fit goes to zero.")]
		private float maxRangeError = 10f;

		[SerializeField]
		[Tooltip("Maximum depth of planned combo chain.")]
		private int maxComboDepth = 3;

		private IMovePerformanceHandler moveHandler;
		private IStunHandler stunHandler;

		private EntityStat powerStat;
		private EntityStat poiseStat;

		public void InjectDependencies(
			AgentStatHandler agentStatHandler,
			IMovePerformanceHandler moveHandler,
			IStunHandler stunHandler)
		{
			StatHandler = agentStatHandler;
			this.moveHandler = moveHandler;
			this.stunHandler = stunHandler;

			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			poiseStat = Agent.Stats.GetStat(AgentStatIdentifiers.POISE);
		}

		protected void OnEnable()
		{
			Agent.SubscribeOptimizedUpdate(OnUpdate);
		}

		protected void OnDisable()
		{
			Agent.UnsubscribeOptimizedUpdate(OnUpdate);
		}

		/// <summary>
		/// Invoke frequency depends on Agent's update priority.
		/// </summary>
		protected void OnUpdate(float deltaTime)
		{
			InCombatMode = Agent.Brain.IsStateActive(AgentStateIdentifiers.COMBAT);

			CurrentCombatMove =
				Agent.Actor.MainPerformer is MovePerformer performer &&
				performer.Move is ICombatMove combatMove
					? combatMove
					: null;

			// Openness: the easier you can be staggered (low Endurance), the more "open" you are.
			Openness = !InCombatMode || Stunned ? 1f
				: (1f / StatHandler.PointStats.W.Cost).InvertClamped();

			// Base reach: global REACH + best hand reach, as before.
			BaseReach =
				(Agent.Stats.GetStat(AgentStatIdentifiers.REACH) ?? fallbackBaseReach) +
				Mathf.Max(
					Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_LEFT_HAND)) ?? 0f,
					Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_RIGHT_HAND)) ?? 0f);

			// LETHALITY: Damage output.
			// TODO: Should depend on currently equiped arms and their damage type; magic weapons won't use physical offense & power.
			// Offense
			Offense =
				Mathf.Max(
					Agent.Stats.GetStat(AgentStatIdentifiers.PIERCE.SubStat(AgentStatIdentifiers.SUB_LEFT_HAND)) ?? 0f,
					Agent.Stats.GetStat(AgentStatIdentifiers.PIERCE.SubStat(AgentStatIdentifiers.SUB_RIGHT_HAND)) ?? 0f);

			UpdatePreferredMove();
		}

		private void UpdatePreferredMove()
		{
			PreferredAct = null;
			PreferredMove = null;
			PreferredMoveReach = BaseReach;
			PreferredMoveInputChain = Array.Empty<string>();

			if (!InCombatMode ||
				Agent.Targeter.Target == null ||
				moveHandler == null ||
				moveHandler.Moveset == null ||
				moveHandler.Moveset.Count == 0)
			{
				return;
			}

			ITargetable target = Agent.Targeter.Target;
			float distance = Vector3.Distance(Agent.Transform.position, target.Position);

			Vector8 pers = Agent.Mind.Personality;
			Vector8 dev = (pers - Vector8.Half) * 2f; // [-1..1]

			// Risk appetite (FIERCENESS vs CAREFULNESS vs SERIOUSNESS).
			float riskBias = Mathf.Clamp01(
				0.5f +
				0.25f * dev.N - // FIERCENESS
				0.25f * dev.S - // CAREFULNESS
				0.25f * dev.W   // SERIOUSNESS
			);

			// Speed preference (SWIFTNESS).
			float swiftnessPref = Mathf.Clamp01(0.5f + 0.5f * dev.E);

			// Charge / Static preference (SHARPNESS).
			float chargePref = Mathf.Clamp01(0.5f + 0.5f * dev.NE);

			// Aptness: high -> more deterministic, low -> more noisy.
			float aptness = Mathf.Clamp01(0.5f + 0.5f * dev.SW);

			float bestScore = -1f;
			string bestAct = null;
			ICombatMove bestMove = null;
			float bestReach = BaseReach;
			string[] bestChain = Array.Empty<string>();

			foreach (KeyValuePair<string, IPerformanceMove> entry in moveHandler.Moveset)
			{
				string rootAct = entry.Key;
				IPerformanceMove rootMove = entry.Value;
				if (rootMove == null)
				{
					continue;
				}

				// Build a simple "best" combo chain starting from this root.
				BuildBestComboChain(
					rootMove,
					rootAct,
					maxComboDepth,
					out string[] chain,
					out ICombatMove finalMove);

				// We only care about chains that end in a combat move.
				if (finalMove == null)
				{
					continue;
				}

				ICombatMove evalMove = finalMove;
				float reach = ComputeEffectiveReach(evalMove); // NO storm here.

				// === RANGE FIT ===
				float distError = Mathf.Abs(distance - reach);
				float rangeScore = Mathf.InverseLerp(maxRangeError, 0f, distError);

				// Timing.
				float chargeSpeed = Agent.Stats.GetStat(evalMove.ChargeSpeedMultiplierStat) ?? 1f;
				float performSpeed = Agent.Stats.GetStat(evalMove.PerformSpeedMultiplierStat) ?? 1f;
				float invChargeSpeed = 1f / Mathf.Max(chargeSpeed, 0.01f);
				float invPerformSpeed = 1f / Mathf.Max(performSpeed, 0.01f);

				float chargeTime = evalMove.HasCharge ? evalMove.MinCharge * invChargeSpeed : 0f;
				float performTime = evalMove.HasPerformance ? evalMove.MinDuration * invPerformSpeed : 0f;
				float totalTime = chargeTime + performTime;
				float speedFactor = 1f / (1f + totalTime);

				// Cost preference.
				float costScore = Mathf.Clamp01(EvaluateCost(evalMove.ChargeCost) * EvaluateCost(evalMove.PerformCost));

				// Static / storm potential (later for damage/crit tendencies, not reach).
				float staticAvail = StatHandler != null ? StatHandler.PointStats.NE.PercentageRecoverable : 0f;
				float stormPotential = 0f;
				if (evalMove is IMeleeCombatMove melee && melee.StormDistance > 0f && staticAvail > 0f)
				{
					float stormNorm = melee.StormDistance / (melee.StormDistance + 1f);
					stormPotential = stormNorm * staticAvail;
				}
				float chargeFactor = 1f + stormPotential;

				float score = rangeScore;

				// Speed preference (SWIFTNESS).
				score *= Mathf.Lerp(1f, speedFactor, swiftnessPref);

				// Cost sensitivity: cautious / serious personalities care more about cost.
				score *= Mathf.Lerp(1f, costScore, 1f - riskBias);

				// Static / charge preference (SHARPNESS + Static).
				score *= Mathf.Lerp(1f, chargeFactor, chargePref);

				// Aptness: low -> noisy, high -> stable.
				float rand = Mathf.Lerp(0.5f, 1.5f, UnityEngine.Random.value);
				score *= Mathf.Lerp(rand, 1f, aptness);

				// -----------------------------
				// Combo depth preference
				// -----------------------------
				int extraSteps = Mathf.Clamp(chain.Length - 1, 0, maxComboDepth);
				if (extraSteps > 0)
				{
					// Depth in [0..1]: 0 = single, 1 = maxDepth chain.
					float depthNorm = (float)extraSteps / maxComboDepth;

					// Personality drives for combos:
					// - FIERCENESS (N), SHARPNESS (NE), RUTHLESSNESS (NW) favor deeper chains.
					// - CAREFULNESS (S), STEADFASTNESS (W) resist long commitment.
					float fierceness = dev.N;
					float sharpness = dev.NE;
					float ruthlessness = dev.NW;
					float carefulness = dev.S;
					float steadfast = dev.W;

					// Average "combo aggression" and "combo caution".
					float comboAgg = (fierceness + sharpness + ruthlessness) / 3f;   // [-1..1]
					float comboCaut = (carefulness + steadfast) * 0.5f;               // [-1..1]

					// Raw taste: high when (aggression + sharp/sharp + ruthless) >> (careful + steadfast).
					float rawTaste = comboAgg - comboCaut;                             // roughly [-2..2]

					// Map to [0..1]; 0.5 = neutral, >0.5 likes deeper combos, <0.5 dislikes.
					float comboTaste = Mathf.Clamp01(0.5f + 0.25f * rawTaste);

					// Baseline: combos slightly rarer at neutral personality.
					const float baselineBias = -0.15f; // mild penalty at full depth.

					// Personality can push this towards more penalty or slight bonus.
					// comboTaste = 0  -> -0.25, comboTaste = 1 -> +0.25.
					float tasteBias = Mathf.Lerp(-0.25f, 0.25f, comboTaste);          // [-0.25..0.25]
					float depthBias = baselineBias + tasteBias;                       // roughly [-0.4..0.1]
					depthBias = Mathf.Clamp(depthBias, -0.4f, 0.15f);

					// Scale by depth: deeper chains more affected.
					float depthFactor = 1f + depthBias * depthNorm;
					score *= depthFactor;
				}


				if (score > bestScore)
				{
					bestScore = score;
					bestAct = rootAct;
					bestMove = evalMove;
					bestReach = reach;
					bestChain = chain;
				}
			}

			if (bestMove != null && bestChain.Length > 0)
			{
				PreferredAct = bestAct;
				PreferredMove = bestMove;
				PreferredMoveReach = bestReach;
				PreferredMoveInputChain = bestChain;
			}
		}

		private float EvaluateCost(StatCost cost)
		{
			if (!cost.Required || string.IsNullOrEmpty(cost.Stat) || StatHandler == null)
			{
				return 1f;
			}

			float pool = Agent.Stats.GetStat(cost.Stat) ?? 0f;
			float unitCost = cost.Cost * (Agent.Stats.GetStat(cost.Stat.SubStat(AgentStatIdentifiers.SUB_COST)) ?? 1f);

			if (unitCost <= 0f) return 1f;
			if (pool <= 0f) return 0f;

			float r = pool / unitCost;
			return Mathf.Clamp01(r / (1f + r));
		}

		/// <summary>
		/// Builds a "best" combo chain based purely on follow-up priorities and melee power/offence.
		/// </summary>
		private void BuildBestComboChain(
			IPerformanceMove rootMove,
			string rootAct,
			int maxDepth,
			out string[] chain,
			out ICombatMove finalMove)
		{
			List<string> acts = new List<string> { rootAct };
			HashSet<IPerformanceMove> visited = new HashSet<IPerformanceMove>();
			IPerformanceMove current = rootMove;
			finalMove = current as ICombatMove;

			for (int depth = 0; depth < maxDepth; depth++)
			{
				if (current == null || visited.Contains(current) || current.FollowUps == null || current.FollowUps.Count == 0)
				{
					break;
				}

				visited.Add(current);

				MoveFollowUp bestFU = null;
				float bestFUScore = -1f;

				for (int i = 0; i < current.FollowUps.Count; i++)
				{
					MoveFollowUp fu = current.FollowUps[i];
					if (fu == null || fu.Move == null || string.IsNullOrEmpty(fu.Act))
					{
						continue;
					}

					float score = fu.Prio;

					if (fu.Move is ICombatMove fuCombat)
					{
						if (fuCombat is IMeleeCombatMove melee)
						{
							score += melee.Power + melee.Offence;
						}
						else
						{
							score += 1f;
						}
					}

					if (score > bestFUScore)
					{
						bestFUScore = score;
						bestFU = fu;
					}
				}

				if (bestFU == null)
				{
					break;
				}

				acts.Add(bestFU.Act);
				current = bestFU.Move;
				if (current is ICombatMove cm)
				{
					finalMove = cm;
				}
			}

			chain = acts.ToArray();
		}

		/// <summary>
		/// Approximates the effective melee reach of a combat move *without* storm:
		/// base reach + move range + limb reach (for melee).
		/// Storm distance is handled by charge behaviours, not by reach.
		/// </summary>
		private float ComputeEffectiveReach(ICombatMove move)
		{
			float reach = BaseReach + move.Range;

			if (move is IMeleeCombatMove meleeMove)
			{
				float limbReach = Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(meleeMove.Limb)) ?? 0f;
				reach += limbReach;
			}

			return reach;
		}
	}
}
