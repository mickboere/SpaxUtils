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

		/// <summary>True when the agent is in a committed state (stunned or charging) and cannot freely act.</summary>
		public bool IsVulnerable => Stunned || (CurrentCombatMove != null && CurrentCombatMove.HasCharge);

		/// <summary>Normalized recent damage received (0-1, relative to max health). Decays over time.</summary>
		public float RecentDamageNormalized { get; private set; }

		/// <summary>The last entity that inflicted damage on this agent.</summary>
		public IEntity LastAttacker { get; private set; }

		/// <summary>The combat move that is currently being prepared/performed (if any).</summary>
		public ICombatMove CurrentCombatMove { get; private set; }

		/// <summary>How exposed the agent currently is to being hit (0-1).</summary>
		public float Openness { get; private set; }

		/// <summary>
		/// Moveless resting reach: global REACH + the longer hand (best-hand estimate). Used only as the no-move
		/// fallback for <see cref="ActiveReach"/> / <see cref="PreferredMoveReach"/>. The per-move reach does NOT
		/// build on this — it would double-count the acting hand. See <see cref="ComputeEffectiveReach"/>.
		/// </summary>
		public float BaseReach { get; private set; }

		/// <summary>Global REACH stat only (no hands). The per-move reach base, kept separate from <see cref="BaseReach"/>'s best-hand term to avoid double-counting the acting limb.</summary>
		private float globalReach;

		/// <summary>Total reach the agent can actually hit at right now — the active move's limb reach + lunge included (or the resting reach when no move is active).</summary>
		public float ActiveReach => CurrentCombatMove == null ? BaseReach : ComputeEffectiveReach(CurrentCombatMove);

		/// <summary>
		/// The agent's per-axis offensive output (x=Slash, y=Power, z=Pierce) of its dominant hand.
		/// Symmetric with <see cref="Defense"/>; feed into a defender's <see cref="EstimateIncomingDamage"/>.
		/// </summary>
		public Vector3 Offense { get; private set; }

		/// <summary>The current raw POWER stat (e.g. for knockback force / mass coupling), not damage output.</summary>
		public float Power => powerStat.Value;

		/// <summary>Slash-defence stat (also half of blunt defence).</summary>
		public float Armor => armorStat.Value;

		/// <summary>Crit/pierce-defence stat (also half of blunt defence).</summary>
		public float Yield => yieldStat.Value;

		/// <summary>
		/// Per-axis defence (x=vs Slash, y=vs Power, z=vs Pierce), mirroring the hit layers in
		/// <see cref="AgentHitHandlerComponent"/>: Slash←Armor, Power←(Armor+Yield)/2, Pierce←Yield.
		/// </summary>
		public Vector3 Defense => new Vector3(Armor, (Armor + Yield) * 0.5f, Yield);

		/// <summary>Live moveless offensive output through the left-hand weapon (or fists). For UI / queries.</summary>
		public CombatOutput LeftHandOutput { get; private set; }

		/// <summary>Live moveless offensive output through the right-hand weapon (or fists). For UI / queries.</summary>
		public CombatOutput RightHandOutput { get; private set; }

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
		/// Live total reach while charging: <see cref="ActiveReach"/> + the storm distance the CURRENT charge buys
		/// ((ChargeMultiplier − 1) × move StormDistance). Equals ActiveReach for uncharged / non-storm moves.
		/// </summary>
		/// <summary>Live charge multiplier of the active performance (1 = uncharged); pulled from the move performer.</summary>
		public float CurrentChargeMultiplier => moveHandler != null ? moveHandler.ChargeMultiplier : 1f;

		public float CurrentStormReach
		{
			get
			{
				float reach = ActiveReach;
				if (CurrentCombatMove is IMeleeCombatMove melee)
				{
					reach += Mathf.Max(0f, CurrentChargeMultiplier - 1f) * melee.StormDistance;
				}
				return reach;
			}
		}

		/// <summary>
		/// Furthest the agent could storm RIGHT NOW given its current Static: <see cref="PreferredMoveReach"/> plus
		/// the max storm distance fundable by draining all of NE (capped at the charge-multiplier ceiling). Used by
		/// the Storm behaviour's Valid gate — no point entering Storm if this can't reach the target.
		/// </summary>
		public float ProjectedStormReach
		{
			get
			{
				float reach = PreferredMoveReach;
				if (PreferredMove is IMeleeCombatMove melee && combatSettings != null && StatHandler != null)
				{
					float staticBudget = StatHandler.PointStats.NE;
					float maxExtra = Mathf.Max(0f, Mathf.Min(combatSettings.MaxChargeMultiplier - 1f, staticBudget * combatSettings.ChargeConversionRatio));
					reach += maxExtra * melee.StormDistance;
				}
				return reach;
			}
		}

		/// <summary>
		/// Projected heavy-arms wield speed factor for <paramref name="move"/> at the agent's CURRENT strength vs the
		/// limb+weapon mass — the same multiplier the move applies mid-swing (<see cref="CombatSettings.WieldSpeedFactor"/>),
		/// but readable BEFORE the swing so move-selection and the AI's strike-timing model the real (slower) heavy swing
		/// instead of the un-penalised base speed. Natural strikes (kick/ram — no limb mass) → 1. 1 when no settings.
		/// </summary>
		public float WieldSpeedFactor(IPerformanceMove move)
		{
			return combatSettings != null ? combatSettings.WieldSpeedFactor(WieldRatio(move)) : 1f;
		}

		/// <summary>
		/// Strength / limb-mass wield ratio for <paramref name="move"/> (mirrors the performer): a limbed/armed move
		/// uses the limb's MASS substat (with any equipped weapon folded in); a natural strike with no such substat is
		/// neutral (1), so a kick is never penalised by a heavy weapon it doesn't wield.
		/// </summary>
		private float WieldRatio(IPerformanceMove move)
		{
			if (move is not IMeleeCombatMove melee)
			{
				return 1f;
			}
			EntityStat limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(melee.Limb));
			if (limbMassStat == null)
			{
				return 1f; // natural strike: no limb/weapon mass to wield.
			}
			float mass = (float)limbMassStat;
			if (mass <= 0f)
			{
				return 1f;
			}
			float strength = Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH) ?? 1f;
			float ratio = strength / mass;
			return ratio < 0f ? 0f : ratio;
		}

		/// <summary>
		/// Planned input chain (acts) to reach <see cref="PreferredMove"/>.
		/// First entry is the opening act.
		/// </summary>
		public string[] PreferredMoveInputChain { get; private set; } = Array.Empty<string>();

		#endregion Move Selection

		[Header("Damage Tracking")]
		[SerializeField] private float recentDamageDecay = 2f;

		[Header("Reach")]
		[SerializeField] private float fallbackBaseReach = 0.5f;

		[SerializeField]
		[Tooltip("Maximum distance error (world units) at which range fit reaches zero. Lower = range dominates selection (2–4m recommended). At 10m range is nearly irrelevant.")]
		private float maxRangeError = 10f;

		[SerializeField]
		[Tooltip("Maximum depth of planned combo chain.")]
		private int maxComboDepth = 3;

		[Header("Move Selection — Output & Variety")]
		[SerializeField, Range(0f, 1f),
		 Tooltip("How strongly a move's damage type must match the agent's NW/N/NE (Slash/Power/Pierce) alignment.")]
		private float alignmentWeight = 0.6f;
		[SerializeField, Range(0.05f, 5f),
		 Tooltip("Body-normalised output-per-second at which the offence factor saturates to 0.5. " +
				 "Level-invariant (DPS is divided by the agent's body magnitude). Lower = raw damage " +
				 "matters more / saturates sooner. Order ~1.")]
		private float offenceHalf = 1f;
		[SerializeField, Range(0f, 1f),
		 Tooltip("Upper clamp on predictability so some selection noise always remains (prevents identical repeats).")]
		private float maxPredictability = 0.85f;
		[SerializeField, Range(0f, 1f),
		 Tooltip("Score penalty applied to the most recently opened root act, decaying over Recency Window.")]
		private float recencyPenalty = 0.35f;
		[SerializeField,
		 Tooltip("Seconds over which the recency penalty on the last-opened root decays back to zero.")]
		private float recencyWindow = 3f;

		[SerializeField, Range(0f, 4f),
		 Tooltip("Windup-risk strength: as the enemy closes, long-charge (committal) moves are de-scored so quick/responsive moves are favored. 0 = off.")]
		private float windupRiskK = 1.5f;
		[SerializeField,
		 Tooltip("Smoothed closing speed (m/s) at which the windup-risk reaches full strength.")]
		private float closingRiskSpeed = 5f;

		[SerializeField, Tooltip("Log per-candidate move-selection score breakdown (throttled ~1s) for debugging.")]
		private bool debugSelection;

		private IMovePerformanceHandler moveHandler;
		private CombatSettings combatSettings;
		private IAgentMovementHandler movementHandler;
		private IStunHandler stunHandler;
		private IHittable hittable;

		private EntityStat powerStat;
		private EntityStat armorStat;
		private EntityStat yieldStat;
		private EntityStat slashStat;
		private EntityStat pierceStat;
		private AgentArmsComponent arms;

		private ICombatMove lastFrameCombatMove;
		private string lastOpenedAct;
		private float lastOpenedTime = -999f;
		private float lastSelectionLogTime = -999f;

		// Self-contained smoothed closing speed (m/s) toward the current target, from the distance this
		// component already tracks each tick (Δdistance/Δtime). Positive = closing in.
		private const float CLOSING_SMOOTH_RATE = 5f; // higher = snappier; ~1/rate s time-constant.
		private float smoothedClosing;
		private float lastClosingDistance;
		private ITargetable closingTarget;

		public void InjectDependencies(
			AgentStatHandler agentStatHandler,
			IMovePerformanceHandler moveHandler,
			IAgentMovementHandler movementHandler,
			IStunHandler stunHandler,
			IHittable hittable,
			CombatSettings combatSettings,
			[Optional] AgentArmsComponent arms)
		{
			StatHandler = agentStatHandler;
			this.moveHandler = moveHandler;
			this.movementHandler = movementHandler;
			this.stunHandler = stunHandler;
			this.hittable = hittable;
			this.combatSettings = combatSettings;
			this.arms = arms;

			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			armorStat = Agent.Stats.GetStat(AgentStatIdentifiers.ARMOR);
			yieldStat = Agent.Stats.GetStat(AgentStatIdentifiers.YIELD);
			slashStat = Agent.Stats.GetStat(AgentStatIdentifiers.SLASH);
			pierceStat = Agent.Stats.GetStat(AgentStatIdentifiers.PIERCE);
		}

		protected void OnEnable()
		{
			Agent.SubscribeOptimizedUpdate(OnUpdate);
			hittable?.Subscribe(this, OnReceivedHit);
		}

		protected void OnDisable()
		{
			Agent.UnsubscribeOptimizedUpdate(OnUpdate);
			hittable?.Unsubscribe(this);
		}

		private void OnReceivedHit(HitData hitData)
		{
			float damage = hitData.Data.GetValue<float>(HitDataIdentifiers.DAMAGE_TOTAL, 0f);
			if (damage <= 0f || StatHandler == null) return;
			float maxHealth = StatHandler.PointStats.SW.Max;
			if (maxHealth > 0f)
				RecentDamageNormalized = Mathf.Clamp01(RecentDamageNormalized + damage / maxHealth);
			LastAttacker = hitData.Hitter;
		}

		/// <summary>
		/// Invoke frequency depends on Agent's update priority.
		/// </summary>
		protected void OnUpdate(float deltaTime)
		{
			RecentDamageNormalized = Mathf.MoveTowards(RecentDamageNormalized, 0f, recentDamageDecay * deltaTime);

			InCombatMode = Agent.Brain.IsStateActive(AgentStateIdentifiers.COMBAT);

			CurrentCombatMove =
				Agent.Actor.MainPerformer is MovePerformer performer &&
				performer.Move is ICombatMove combatMove
					? combatMove
					: null;

			// Openness: the easier you can be staggered (low Endurance), the more "open" you are.
			Openness = !InCombatMode || Stunned ? 1f
				: (1f / StatHandler.PointStats.W.DrainMult).InvertClamped();

			// Global REACH (limb-agnostic). The per-move path adds the acting limb on top of THIS, not BaseReach,
			// so the hand is never counted twice. BaseReach keeps the best-hand term for the moveless resting reach.
			globalReach = Agent.Stats.GetStat(AgentStatIdentifiers.REACH) ?? fallbackBaseReach;
			BaseReach =
				globalReach +
				Mathf.Max(
					Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_LEFT_HAND)) ?? 0f,
					Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_RIGHT_HAND)) ?? 0f);

			// LETHALITY: live moveless damage output per equipped weapon (or fists). Central authority
			// for UI/queries; replaces the old per-hand SLASH-substat approximation.
			LeftHandOutput = new CombatOutput(BodyPhysics, DistributionOf(arms == null ? null : arms.LeftEquip, Vector3.one));
			RightHandOutput = new CombatOutput(BodyPhysics, DistributionOf(arms == null ? null : arms.RightEquip, Vector3.one));
			Offense = (LeftHandOutput.Magnitude >= RightHandOutput.Magnitude ? LeftHandOutput : RightHandOutput).Output;

			// Recency tracking: when a fresh combo opens (no move → move), remember its opener act.
			if (CurrentCombatMove != null && lastFrameCombatMove == null && !string.IsNullOrEmpty(PreferredAct))
			{
				lastOpenedAct = PreferredAct;
				lastOpenedTime = Time.time;
			}
			lastFrameCombatMove = CurrentCombatMove;

			// Self-contained smoothed closing speed toward the target (Δdistance/Δtime) for the windup risk.
			ITargetable closeTo = Agent.Targeter.Target;
			if (closeTo != null)
			{
				float d = Vector3.Distance(Agent.Transform.position, closeTo.Position);
				float rawClosing = closingTarget == closeTo ? (lastClosingDistance - d) / Mathf.Max(deltaTime, 0.0001f) : 0f;
				smoothedClosing = Mathf.Lerp(smoothedClosing, rawClosing, Mathf.Clamp01(CLOSING_SMOOTH_RATE * deltaTime));
				lastClosingDistance = d;
				closingTarget = closeTo;
			}
			else
			{
				smoothedClosing = 0f;
				closingTarget = null;
			}

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

			// High SW balance = adaptive/unpredictable. Low SW balance = consistent/predictable.
			// Clamp so a noise floor always remains (otherwise sharp/low-SW agents become 100% deterministic).
			float predictability = Mathf.Min(1f - Agent.Mind.Balance.SW, maxPredictability);

			// Agent's live damage-type lean (Slash/Power/Pierce) for move alignment, plus the body
			// output magnitude used to make the offence/DPS term level-invariant (it scales with level,
			// so dividing by it cancels the level scaling and keeps offenceHalf meaningful at any level).
			Vector3 agentDir = AlignmentDir();
			float bodyMag = Mathf.Max(BodyPhysics.magnitude, 0.0001f);

			// How hard the enemy is closing (smoothed, 0..1) — drives the windup risk below.
			float closingNorm = Mathf.Clamp01(smoothedClosing / Mathf.Max(closingRiskSpeed, 0.01f));

			// Storm only extends a move's usable reach when the agent has built-up Static to overcharge
			// with — otherwise the long storm reach wrongly marks every move "engageable from afar" and
			// (with maxRangeError) collapses every melee move's range-fit to 0.
			float staticAvail = StatHandler != null ? StatHandler.PointStats.NE.PercentageRecoverable : 0f;
			// Gated by BOTH available Static AND charge inclination (SHARPNESS): a full-Static agent with
			// no intent to overcharge gets no storm-reach credit.
			float stormGate = Mathf.Clamp01(staticAvail) * chargePref;

			bool doLog = debugSelection && Time.time - lastSelectionLogTime > 1f;
			System.Text.StringBuilder sb = doLog ? new System.Text.StringBuilder() : null;

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
					distance,
					predictability,
					stormGate,
					out string[] chain,
					out ICombatMove finalMove);

				// We only care about chains that end in a combat move.
				if (finalMove == null)
				{
					continue;
				}

				ICombatMove evalMove = finalMove;
				float reach = ComputeEffectiveReach(evalMove);

				// === RANGE FIT ===
				// In-range band: a melee move strikes anything within reach, so ONLY being out of reach
				// (too far) is penalised — never being close. This is what stops the score collapsing at
				// point-blank: previously |distance - (storm-inflated reach)| was huge close up → rangeScore
				// 0 → the whole multiplicative score 0 → first-in-dict (Light) always won. maxReach includes
				// storm only when charged & inclined (stormGate).
				// TODO: future ranged weapons supply a min-range so they ARE penalised at point-blank.
				float maxReach = ComputeScoringReach(evalMove, stormGate);
				float outOfRange = Mathf.Max(0f, distance - maxReach);
				float rangeScore = Mathf.InverseLerp(maxRangeError, 0f, outOfRange);

				// Timing. Fold in the heavy-arms wield penalty so a slow heavy swing is SCORED as slow (not as snappy
				// as a light one); natural strikes (kicks) read 1, so they're unaffected.
				float wieldFactor = WieldSpeedFactor(evalMove);
				float chargeSpeed = (Agent.Stats.GetStat(evalMove.ChargeSpeedMultiplierStat) ?? 1f) * wieldFactor;
				float performSpeed = (Agent.Stats.GetStat(evalMove.PerformSpeedMultiplierStat) ?? 1f) * wieldFactor;
				float invChargeSpeed = 1f / Mathf.Max(chargeSpeed, 0.01f);
				float invPerformSpeed = 1f / Mathf.Max(performSpeed, 0.01f);

				float chargeTime = evalMove.HasCharge ? evalMove.MinCharge * invChargeSpeed : 0f;
				float performTime = evalMove.HasPerformance ? evalMove.MinDuration * invPerformSpeed : 0f;
				float totalTime = chargeTime + performTime;
				float speedFactor = 1f / (1f + totalTime);

				// Cost preference.
				float costScore = Mathf.Clamp01(EvaluateCost(evalMove.ChargeCost) * EvaluateCost(evalMove.PerformCost));

				// Static / storm potential (damage/crit tendencies; reuses the hoisted staticAvail).
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

					float rand = Mathf.Lerp(0.5f, 1.5f, UnityEngine.Random.value);
				score *= Mathf.Lerp(rand, 1f, predictability);

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

				// Damage-type alignment: prefer moves whose output axis matches the agent's lean.
				// Weapon-negated axes drop out of the move's profile, so they earn no affinity.
				MoveOutput moveOutput = GetMoveOutput(evalMove);
				float align = Mathf.Clamp01(Vector3.Dot(moveOutput.TypeDirection, agentDir));
				score *= Mathf.Lerp(1f, align, alignmentWeight);

				// Offence: output-per-second normalised by body magnitude, so the saturation point is
				// level-invariant. Makes hard-hitting moves competitive; fierce minds weight it more.
				float dpsNorm = moveOutput.Magnitude / (bodyMag * Mathf.Max(totalTime, 0.0001f));
				float offenceNorm = dpsNorm / (dpsNorm + Mathf.Max(offenceHalf, 0.0001f));
				score *= Mathf.Lerp(1f, offenceNorm, riskBias);

				// Windup risk: while the enemy closes in, de-score long-charge (committal) moves so quick
				// responsive moves win — avoids committing a slow wind-up from afar that gets baited/dodged.
				if (windupRiskK > 0f && closingNorm > 0f)
				{
					float windupSafety = 1f / (1f + chargeTime * windupRiskK);
					score *= Mathf.Lerp(1f, windupSafety, closingNorm);
				}

				// Recency penalty: nudge away from repeating the just-opened root (decays over recencyWindow).
				if (recencyPenalty > 0f && rootAct == lastOpenedAct)
				{
					float decay01 = recencyWindow > 0f ? Mathf.Clamp01((Time.time - lastOpenedTime) / recencyWindow) : 1f;
					score *= Mathf.Lerp(1f - recencyPenalty, 1f, decay01);
				}

				if (doLog)
				{
					sb.AppendLine($"  {rootAct} [{(evalMove as UnityEngine.Object)?.name ?? evalMove?.GetType().Name}] score={score:F3} | range={rangeScore:F2}(reach={ComputeScoringReach(evalMove, stormGate):F2} d={distance:F2}) speed={speedFactor:F2} align={align:F2} dps={offenceNorm:F2} chain={chain.Length}");
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

			if (doLog)
			{
				lastSelectionLogTime = Time.time;
				SpaxDebug.Log("MoveSelect", $"{Agent.Identification.ID} dist={distance:F2} best={bestAct ?? "none"}\n{sb}");
			}
		}

		private float EvaluateCost(StatCost cost)
		{
			if (!cost.Required || string.IsNullOrEmpty(cost.Stat) || StatHandler == null)
			{
				return 1f;
			}

			float pool = Agent.Stats.GetStat(cost.Stat) ?? 0f;
			float unitCost = cost.Cost * (Agent.Stats.GetStat(cost.Stat.SubStat(AgentStatIdentifiers.SUB_DRAIN)) ?? 1f);

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
			float distance,
			float predictability,
			float stormGate,
			out string[] chain,
			out ICombatMove finalMove)
		{
			List<string> acts = new List<string> { rootAct };
			HashSet<IPerformanceMove> visited = new HashSet<IPerformanceMove>();
			IPerformanceMove current = rootMove;
			finalMove = current as ICombatMove;

			Vector3 agentDir = AlignmentDir();

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
							score += melee.Power + melee.Slash;
						}
						else
						{
							score += 1f;
						}

						float fuRangeFit = Mathf.InverseLerp(maxRangeError, 0f, Mathf.Abs(distance - ComputeScoringReach(fuCombat, stormGate)));
						score *= Mathf.Lerp(0.5f, 1f, fuRangeFit);

						// Deterministic damage-type alignment toward the agent's lean.
						float fuAlign = Mathf.Clamp01(Vector3.Dot(GetMoveOutput(fuCombat).TypeDirection, agentDir));
						score *= Mathf.Lerp(1f, fuAlign, alignmentWeight);
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
		/// global REACH + move range + acting-limb reach + lunge distance (for melee).
		/// Builds on <see cref="globalReach"/> (not <see cref="BaseReach"/>) so the acting limb is the only hand
		/// term — no double-count. The lunge is the move's own forward inertia braked through the agent's
		/// load/mass, so a heavy agent reaches less far than a light one. Storm is handled by charge behaviours.
		/// </summary>
		private float ComputeEffectiveReach(ICombatMove move)
		{
			float reach = globalReach + move.Range;

			if (move is IMeleeCombatMove meleeMove)
			{
				float limbReach = Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(meleeMove.Limb)) ?? 0f;
				float lunge = movementHandler != null ? movementHandler.PredictBrakingDistance(meleeMove.Inertia.z) : 0f;
				reach += limbReach + lunge;
			}

			return reach;
		}

		// Reach used for range-fit scoring. Storm distance is only added in proportion to <paramref name="stormGate"/>
		// (the agent's built-up Static), so a move only counts as "engageable from afar" when it can actually
		// overcharge-dash. With stormGate = 0 this equals the storm-free effective reach.
		private float ComputeScoringReach(ICombatMove move, float stormGate)
		{
			float reach = ComputeEffectiveReach(move);
			if (move is IMeleeCombatMove melee)
			{
				reach += melee.StormDistance * Mathf.Clamp01(stormGate);
			}
			return reach;
		}

		#region Combat Output

		/// <summary>
		/// Moveless offensive output of the agent through a given weapon (or fists): the body's per-axis
		/// physics (x=Slash, y=Power, z=Pierce) scaled by the weapon's distribution (NW/N/NE).
		/// </summary>
		public readonly struct CombatOutput
		{
			/// <summary>Body offensive physics (x=Slash, y=Power, z=Pierce).</summary>
			public readonly Vector3 BodyPhysics;
			/// <summary>Weapon distribution (x=NW/Slash, y=N/Power, z=NE/Pierce) * PhysicsScaling; (1,1,1) = fists.</summary>
			public readonly Vector3 WeaponDistribution;
			/// <summary>Per-axis output = BodyPhysics ⊙ WeaponDistribution.</summary>
			public readonly Vector3 Output;

			public CombatOutput(Vector3 bodyPhysics, Vector3 weaponDistribution)
			{
				BodyPhysics = bodyPhysics;
				WeaponDistribution = weaponDistribution;
				Output = Vector3.Scale(bodyPhysics, weaponDistribution);
			}

			public float Slash => Output.x;
			public float Power => Output.y;
			public float Pierce => Output.z;
			public float Magnitude => Output.magnitude;
		}

		/// <summary>
		/// Move-specific offensive output: the move's slider distribution combined with the equipped weapon,
		/// normalised into a damage-type filter and applied to body physics. Mirrors the combine in
		/// <see cref="MeleeCombatBehaviourAsset"/> so that asset reads this instead of recomputing it.
		/// </summary>
		public readonly struct MoveOutput
		{
			/// <summary>The moveless weapon output this derives from.</summary>
			public readonly CombatOutput Weapon;
			/// <summary>Move sliders (x=Slash, y=Power, z=Pierce).</summary>
			public readonly Vector3 MoveSliders;
			/// <summary>UseArmament ? weapon⊙move : move (raw, pre-normalisation).</summary>
			public readonly Vector3 CombinedRaw;
			/// <summary>Normalised damage-type direction (×maxSlider for special >1 finishers).</summary>
			public readonly Vector3 Filter;
			/// <summary>Per-axis base output = BodyPhysics ⊙ Filter (pre charge/phase/strength).</summary>
			public readonly Vector3 Output;

			public MoveOutput(CombatOutput weapon, Vector3 moveSliders, Vector3 combinedRaw, Vector3 filter, Vector3 output)
			{
				Weapon = weapon;
				MoveSliders = moveSliders;
				CombinedRaw = combinedRaw;
				Filter = filter;
				Output = output;
			}

			public float Slash => Output.x;
			public float Power => Output.y;
			public float Pierce => Output.z;
			public float Magnitude => Output.magnitude;
			/// <summary>Normalised output direction in (Slash, Power, Pierce) space, for alignment.</summary>
			public Vector3 TypeDirection => Filter.sqrMagnitude > 0f ? Filter.normalized : Vector3.zero;
		}

		/// <summary>Body offensive physics as (x=Slash, y=Power, z=Pierce).</summary>
		private Vector3 BodyPhysics => new Vector3(slashStat ?? 0f, powerStat ?? 0f, pierceStat ?? 0f);

		/// <summary>The agent's live NW/N/NE (Slash/Power/Pierce) damage-type lean from Balance, normalised.</summary>
		private Vector3 AlignmentDir()
		{
			Vector8 b = Agent.Mind.Balance;
			Vector3 v = new Vector3(b.NW, b.N, b.NE);
			return v.sqrMagnitude > 0f ? v.normalized : Vector3.zero;
		}

		// (NW, N, NE) * PhysicsScaling for an equipment data; fallback when none.
		private static Vector3 DistributionOf(IEquipmentData data, Vector3 fallback)
		{
			if (data == null)
			{
				return fallback;
			}
			Vector8 dis = data.PhysicsDistribution;
			return new Vector3(dis.NW, dis.N, dis.NE) * data.PhysicsScaling;
		}

		private static Vector3 DistributionOf(RuntimeEquipedData weapon, Vector3 fallback)
			=> DistributionOf(weapon == null ? null : weapon.EquipmentData, fallback);

		private RuntimeEquipedData ResolveMoveWeapon(ICombatMove move)
		{
			if (arms == null || move is not IMeleeCombatMove melee || !melee.UseArmament || melee.Limb.IsNullOrEmpty())
			{
				return null;
			}
			return melee.Limb == EquipmentSlotTypes.LEFT_HAND ? arms.LeftEquip : arms.RightEquip;
		}

		/// <summary>Moveless output for any (incl. hypothetical) weapon — for UI weapon comparison.</summary>
		public CombatOutput GetWeaponOutput(IEquipmentData weapon)
			=> new CombatOutput(BodyPhysics, DistributionOf(weapon, Vector3.one));

		/// <summary>Output of <paramref name="move"/> using the weapon currently equipped on its limb.</summary>
		public MoveOutput GetMoveOutput(ICombatMove move) => GetMoveOutput(move, ResolveMoveWeapon(move));

		/// <summary>
		/// Output of <paramref name="move"/> with a specific (possibly hypothetical) weapon. Replicates the
		/// combine in <see cref="MeleeCombatBehaviourAsset"/> exactly so the hit pipeline stays unchanged.
		/// </summary>
		public MoveOutput GetMoveOutput(ICombatMove move, RuntimeEquipedData weapon)
		{
			Vector3 body = BodyPhysics;

			if (move is not IMeleeCombatMove melee)
			{
				// Non-melee combat move — neutral fallback; extension seam for future ranged/magic.
				Vector3 neutral = new Vector3(1f, 1f, 1f).normalized;
				return new MoveOutput(new CombatOutput(body, Vector3.one), Vector3.one, Vector3.one, neutral, body);
			}

			Vector3 moveDist = new Vector3(melee.Slash, melee.Power, melee.Pierce);
			Vector3 weaponDist = melee.UseArmament ? DistributionOf(weapon, Vector3.zero) : Vector3.one;
			Vector3 combinedRaw = melee.UseArmament ? Vector3.Scale(weaponDist, moveDist) : moveDist;
			float filterMag = combinedRaw.magnitude;
			Vector3 filter = filterMag > 0f ? combinedRaw / filterMag : Vector3.zero;

			// Move sliders >1 are scalar multipliers for special/finisher attacks.
			float maxSlider = Mathf.Max(moveDist.x, moveDist.y, moveDist.z);
			if (maxSlider > 1f)
			{
				filter *= maxSlider;
			}

			Vector3 output = Vector3.Scale(body, filter);
			return new MoveOutput(new CombatOutput(body, melee.UseArmament ? weaponDist : Vector3.one), moveDist, combinedRaw, filter, output);
		}

		/// <summary>
		/// Heuristic estimate of the raw physics damage an incoming <paramref name="offence"/> (x=Slash,
		/// y=Power, z=Pierce) would deal to THIS agent, mirroring <see cref="AgentHitHandlerComponent"/>'s
		/// per-layer defence mapping via <see cref="SpaxFormulas.CalculateDamage"/>. Simplified for AI threat
		/// assessment: ignores crit-chance and the impact/penetration coupling on the blunt layer, so it reads
		/// as a representative (upper-ish) threat rather than an exact expected value.
		/// </summary>
		public float EstimateIncomingDamage(Vector3 offence)
		{
			Vector3 def = Defense;
			return SpaxFormulas.CalculateDamage(offence.x, def.x)   // Slash  vs Armor
				 + SpaxFormulas.CalculateDamage(offence.y, def.y)   // Power  vs (Armor+Yield)/2
				 + SpaxFormulas.CalculateDamage(offence.z, def.z);  // Pierce vs Yield
		}

		#endregion Combat Output
	}
}
