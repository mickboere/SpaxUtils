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

		/// <summary>
		/// <see cref="BaseReach"/> plus the lunge any attack would carry — what an observer should space against,
		/// since an idle enemy is never really limited to its resting reach. Thrust-free (no move chosen yet).
		/// </summary>
		public float RestingThreatReach => BaseReach + StickRange;

		/// <summary>Total reach the agent can actually hit at right now — the active move's limb reach + lunge included (or the resting threat reach when no move is active).</summary>
		public float ActiveReach => CurrentCombatMove == null ? RestingThreatReach : ComputeEffectiveReach(CurrentCombatMove);

		/// <summary>
		/// 0-1 lunge commitment for the next attack: the player's sprint axis, or the AI's choice. 0 = the free lunge.
		/// </summary>
		public float LungeIntent { get; set; }

		/// <summary>
		/// <see cref="ActiveReach"/> as an observer judges it: 1 respects the full bought lunge, 0 only the free one.
		/// </summary>
		public float ThreatReach(float caution)
		{
			float stick = CurrentCombatMove == null ? StickRange : ComputeStickRange(CurrentCombatMove);
			float free = combatSettings == null ? 1f : Mathf.Clamp01(combatSettings.StickFreeFraction);
			return ActiveReach - stick * (1f - free) * (1f - Mathf.Clamp01(caution));
		}

		/// <summary>The current raw POWER stat (e.g. for knockback force / mass coupling), not damage output.</summary>
		public float Power => powerStat.Value;

		/// <summary>Slash-defence stat (also half of blunt defence).</summary>
		public float Armor => armorStat.Value;

		/// <summary>Crit/pierce-defence stat (also half of blunt defence).</summary>
		public float Yield => yieldStat.Value;

		/// <summary>This agent as a target: both walls plus the anatomy that shapes blunt and crit.</summary>
		public DefenceData DefenceProfile =>
			new DefenceData(Armor, Yield, hardnessStat ?? 0.5f, vulnerabilityStat ?? 0.5f, luckStat ?? 0f);

		/// <summary>How much guard this agent is currently holding up (0 when not guarding).</summary>
		public float GuardWeight =>
			Mathf.Clamp01(Agent.RuntimeData.GetValue<float>(AgentDataIdentifiers.GUARD_WEIGHT, 0f));

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

		/// <summary>Live charge multiplier of the active performance (1 = uncharged); pulled from the move performer.</summary>
		public float CurrentChargeMultiplier => moveHandler != null ? moveHandler.ChargeMultiplier : 1f;

		/// <summary>How charged the active performance is, 0..1 of what the Static pool could fund.</summary>
		public float CurrentChargeFraction => moveHandler != null ? moveHandler.ChargeFraction : 0f;

		/// <summary>
		/// Fraction of a full charge the CURRENT Static could still buy, 0..1 — a drained pool can only
		/// fund a partial storm. Full = what the pool at max would store.
		/// </summary>
		public float FundableChargeFraction
		{
			get
			{
				if (combatSettings == null || StatHandler == null)
				{
					return 0f;
				}

				ResourceStat stat = StatHandler.ResourceStats.NE;
				float reference = CombatUtils.ChargeReference(stat.Max, combatSettings.ChargeEfficiencyDecay);
				float ceiling = CombatUtils.ChargeCeiling(stat.Max, reference);
				return ceiling > 0f
					? Mathf.Clamp01(CombatUtils.ChargeCeiling(stat.Value, reference) / ceiling)
					: 0f;
			}
		}

		/// <summary>
		/// Live total reach while charging: <see cref="ActiveReach"/> + the storm distance the CURRENT charge buys.
		/// Equals ActiveReach for uncharged moves.
		/// </summary>
		public float CurrentStormReach => ActiveReach + ComputeStormRange(CurrentCombatMove, CurrentChargeFraction);

		/// <summary>
		/// Furthest the agent could storm RIGHT NOW given its current Static: <see cref="PreferredMoveReach"/> plus
		/// the storm distance draining all of NE would fund. Used by
		/// the Storm behaviour's Valid gate — no point entering Storm if this can't reach the target.
		/// </summary>
		public float ProjectedStormReach
		{
			get
			{
				float reach = PreferredMoveReach;
				if (PreferredMove is IMeleeCombatMove && combatSettings != null && StatHandler != null)
				{
					reach += ComputeStormRange(PreferredMove, FundableChargeFraction);
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
			return combatSettings != null
				? combatSettings.WieldSpeedFactor(Strength, WieldWeaponMass(move)) : 1f;
		}

		/// <summary>
		/// The limb's swing speed as <paramref name="move"/> reaches contact: wield speed times the heavy slow start.
		/// What the performer hands its hit, predicted before the swing.
		/// </summary>
		public float ContactSwingSpeed(IPerformanceMove move)
		{
			if (combatSettings == null || move is not IMeleeCombatMove melee)
			{
				return 1f;
			}
			float shortfall = combatSettings.WieldShortfall(Strength, WieldWeaponMass(move));
			float phase = CombatSettings.SwingPhase(melee.ContactTime, melee.MinDuration);
			return WieldSpeedFactor(move) * combatSettings.PhaseSpeedFactor(shortfall, phase);
		}

		/// <summary>
		/// Weapon mass <paramref name="move"/> wields, the arm's own share of the body removed — what the
		/// too-heavy check measures against Strength. A natural strike (kick, ram) carries none, so it reads 0.
		/// </summary>
		private float WieldWeaponMass(IPerformanceMove move)
		{
			float mass = LimbMass(move);
			if (mass <= 0f)
			{
				return 0f;
			}
			return SpaxFormulas.WeaponMass(mass, Agent.Stats.GetStat(AgentStatIdentifiers.MASS) ?? 0f);
		}

		/// <summary>Strength available to wield with; 1 when the stat is missing.</summary>
		private float Strength => Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH) ?? 1f;

		/// <summary>
		/// Limb+weapon mass <paramref name="move"/> swings, mirroring the performer's own lookup. Zero for a natural
		/// strike (kick, body ram) or a non-melee move — neither carries a limb MASS substat.
		/// </summary>
		private float LimbMass(IPerformanceMove move)
		{
			if (move is not IMeleeCombatMove melee)
			{
				return 0f;
			}
			EntityStat limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(melee.Limb));
			return limbMassStat == null ? 0f : Mathf.Max(0f, (float)limbMassStat);
		}

		/// <summary>
		/// Limb+weapon mass blended toward whole-body by the move's BodyMassFraction — what the strike weighs.
		/// Shared by the hit pipeline and the exertion cost so they can never disagree about a strike's mass.
		/// </summary>
		public float ComputeStrikeMass(IPerformanceMove move)
		{
			if (move is not IMeleeCombatMove melee)
			{
				return 0f;
			}
			float bodyMass = Agent.Stats.GetStat(AgentStatIdentifiers.MASS) ?? 0f;
			return Mathf.Lerp(LimbMass(move), bodyMass, melee.BodyMassFraction);
		}

		/// <summary>Limb+weapon mass <paramref name="move"/> swings, unblended with the body; 0 for limb-less strikes.</summary>
		public float ComputeLimbMass(IPerformanceMove move) => LimbMass(move);

		/// <summary>
		/// What performing <paramref name="move"/> costs its cost-stat: the authored PerformCost priced by the mass it
		/// swings (<see cref="CombatSettings.ExertionFactor"/>). THE authority — the performer drains this, and
		/// move-selection and the AI's affordability gate read it before the swing so all three agree.
		/// Raw, pre-<c>SUB/Drain</c>: this is the value handed to <c>ResourceStat.Drain</c>, which applies it.
		/// </summary>
		/// <summary>The agent's body rank, which sets the mass a strike of theirs is expected to carry.</summary>
		public float Rank => Agent.Stats.GetStat(AgentStatIdentifiers.BODY_RANK) ?? 0f;

		public float ComputePerformCost(IPerformanceMove move)
		{
			if (move?.PerformCost == null)
			{
				return 0f;
			}

			float cost = move.PerformCost.Cost;
			if (move is not IMeleeCombatMove melee || combatSettings == null)
			{
				// Non-melee (spells, projectiles) pay their authored cost flat — nothing bodily is being swung.
				return cost;
			}

			// TWO LANES. An armed strike is priced on the implement it wields; an unarmed one has no implement, so it
			// is priced on what the body itself commits — which is exactly what StrikeMass already measures.
			// Both are judged against the mass their RANK expects, so swings per bar hold as gear grows.
			float rank = Rank;
			float factor = melee.UseArmament
				? combatSettings.ExertionFactor(LimbMass(move),
					SpaxFormulas.ExpectedLimbMass(rank, SpaxFormulas.EXERTION_GEAR_SHARE))
				: combatSettings.ExertionFactor(ComputeStrikeMass(move), SpaxFormulas.ExpectedBodyMass(rank));

			// Authored cost is a SHARE of the pool it drains: 1 empties the bar at parity, at any rank.
			return cost * PoolMax(move.PerformCost.Stat) * factor;
		}

		/// <summary>Max of the resource <paramref name="stat"/> names; 1 when it names no resource.</summary>
		private float PoolMax(string stat)
		{
			return StatHandler != null && StatHandler.TryGetResourceStat(stat, out ResourceStat resource)
				? Mathf.Max(0f, resource.Max) : 1f;
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
		private IStunHandler stunHandler;
		private IHittable hittable;

		/// <summary>The widest turn anything ever needs: a full reversal.</summary>
		private const float FULL_TURN = 180f;

		private EntityStat powerStat;
		private EntityStat armorStat;
		private EntityStat yieldStat;
		private EntityStat slashStat;
		private EntityStat pierceStat;
		private EntityStat hardnessStat;
		private EntityStat vulnerabilityStat;
		private EntityStat luckStat;
		private AgentArmsComponent arms;

		// Heaviest opener, cached: it only changes with equipment, so an observer needn't rescan every tick.
		private const float HEAVIEST_MOVE_REFRESH = 1f;
		private ICombatMove heaviestMove;
		private float heaviestMoveTime = -999f;

		// Resolved combat component of the current target, for scoring moves against its real defences.
		private ITargetable cachedDefenderTarget;
		private AgentCombatComponent cachedDefender;

		// Reused across selections; offence can only be judged once every candidate's value is known.
		private readonly List<MoveCandidate> candidates = new List<MoveCandidate>();
		private readonly List<float> followUpValues = new List<float>();

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
			IStunHandler stunHandler,
			IHittable hittable,
			CombatSettings combatSettings,
			[Optional] AgentArmsComponent arms)
		{
			StatHandler = agentStatHandler;
			this.moveHandler = moveHandler;
			this.stunHandler = stunHandler;
			this.hittable = hittable;
			this.combatSettings = combatSettings;
			this.arms = arms;

			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			armorStat = Agent.Stats.GetStat(AgentStatIdentifiers.ARMOR);
			yieldStat = Agent.Stats.GetStat(AgentStatIdentifiers.YIELD);
			slashStat = Agent.Stats.GetStat(AgentStatIdentifiers.SLASH);
			pierceStat = Agent.Stats.GetStat(AgentStatIdentifiers.PIERCE);
			hardnessStat = Agent.Stats.GetStat(AgentStatIdentifiers.HARDNESS);
			vulnerabilityStat = Agent.Stats.GetStat(AgentStatIdentifiers.VULNERABILITY);
			luckStat = Agent.Stats.GetStat(AgentStatIdentifiers.LUCK);
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
			if (damage <= 0f || StatHandler == null)
			{
				return;
			}
			float maxHealth = StatHandler.ResourceStats.SW.Max;
			if (maxHealth > 0f)
			{
				RecentDamageNormalized = Mathf.Clamp01(RecentDamageNormalized + damage / maxHealth);
			}
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
				: (1f / StatHandler.ResourceStats.W.DrainMult).InvertClamped();

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
			RuntimeEquipedData leftEquip = arms == null ? null : arms.LeftEquip;
			RuntimeEquipedData rightEquip = arms == null ? null : arms.RightEquip;
			LeftHandOutput = new CombatOutput(OffensivePhysics(leftEquip), DistributionOf(leftEquip, Vector3.one));
			RightHandOutput = new CombatOutput(OffensivePhysics(rightEquip), DistributionOf(rightEquip, Vector3.one));

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

			// Agent's live damage-type lean (Slash/Power/Pierce) for move alignment.
			Vector3 agentDir = AlignmentDir();

			// Who we are actually hitting, and the appetite for their stance: ruthlessness (NW) straight up,
			// so at full it is worth as much as their health. Drive, not Balance — it carries difficulty.
			AgentCombatComponent defender = DefenderOf(target);
			float staggerWeight = Agent.Mind.Drive.NW;
			candidates.Clear();

			// How hard the enemy is closing (smoothed, 0..1) — drives the windup risk below.
			float closingNorm = Mathf.Clamp01(smoothedClosing / Mathf.Max(closingRiskSpeed, 0.01f));

			// Storm only extends a move's usable reach when the agent has built-up Static to overcharge
			// with — otherwise the long storm reach wrongly marks every move "engageable from afar" and
			// (with maxRangeError) collapses every melee move's range-fit to 0.
			float staticAvail = StatHandler != null ? StatHandler.ResourceStats.NE.PercentageRecoverable : 0f;
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
					defender,
					staggerWeight,
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
				float costScore = Mathf.Clamp01(
					EvaluateCost(evalMove.ChargeCost, evalMove.ChargeCost.Cost) *
					EvaluateCost(evalMove.PerformCost, ComputePerformCost(evalMove)));

				// Static / storm potential (damage/crit tendencies; reuses the hoisted staticAvail).
				float stormPotential = 0f;
				if (staticAvail > 0f)
				{
					float stormFull = ComputeStormRange(evalMove, 1f);
					stormPotential = stormFull / (stormFull + 1f) * staticAvail;
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
				float align = Mathf.Clamp01(Vector3.Dot(GetMoveOutput(evalMove).TypeDirection, agentDir));
				score *= Mathf.Lerp(1f, align, alignmentWeight);

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

				// Offence is scored against the target, so it can only be judged once every candidate is in.
				candidates.Add(new MoveCandidate
				{
					Act = rootAct,
					Move = evalMove,
					Chain = chain,
					Reach = reach,
					Score = score,
					Value = StrikeValue(evalMove, defender, staggerWeight) / Mathf.Max(totalTime, 0.0001f),
					RangeScore = rangeScore,
					ScoringReach = maxReach,
					SpeedFactor = speedFactor,
					Align = align
				});
			}

			// Offence, relative to the best this moveset can do to THIS target — level-invariant, and
			// independent of how the damage constants are scaled. Fierce minds weight it more.
			float bestValue = 0f;
			for (int i = 0; i < candidates.Count; i++)
			{
				bestValue = Mathf.Max(bestValue, candidates[i].Value);
			}

			for (int i = 0; i < candidates.Count; i++)
			{
				MoveCandidate candidate = candidates[i];
				float offenceNorm = bestValue > 0f ? candidate.Value / bestValue : 1f;
				float score = candidate.Score * Mathf.Lerp(1f, offenceNorm, riskBias);

				if (doLog)
				{
					string moveName = candidate.Move is UnityEngine.Object obj && obj != null
						? obj.name
						: candidate.Move.GetType().Name;
					sb.AppendLine($"  {candidate.Act} [{moveName}] score={score:F3} | range={candidate.RangeScore:F2}(reach={candidate.ScoringReach:F2} d={distance:F2}) speed={candidate.SpeedFactor:F2} align={candidate.Align:F2} offence={offenceNorm:F2} chain={candidate.Chain.Length}");
				}

				if (score > bestScore)
				{
					bestScore = score;
					bestAct = candidate.Act;
					bestMove = candidate.Move;
					bestReach = candidate.Reach;
					bestChain = candidate.Chain;
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

		/// <summary>One scored candidate, held back until every move's value against the target is known.</summary>
		private struct MoveCandidate
		{
			public string Act;
			public ICombatMove Move;
			public string[] Chain;
			public float Reach;
			public float Score;
			public float Value;
			public float RangeScore;
			public float ScoringReach;
			public float SpeedFactor;
			public float Align;
		}

		/// <summary>
		/// What one strike of <paramref name="move"/> is worth against <paramref name="defender"/>: the share of their
		/// health it takes, plus the share of their stance, priced by what breaking that stance would open up.
		/// </summary>
		private float StrikeValue(ICombatMove move, AgentCombatComponent defender, float staggerWeight)
		{
			if (defender == null || defender.StatHandler == null)
			{
				return 0f;
			}

			StrikeData strike = EstimateStrike(move, true);
			DefenceData defence = defender.DefenceProfile;
			float guard = defender.GuardWeight;
			DamageResult result = DamageResolver.Resolve(strike, defence, guard, combatSettings);

			float healthMax = Mathf.Max(defender.StatHandler.ResourceStats.SW.Max, 0.0001f);
			float health = result.ExpectedHealth / healthMax;

			// What a stun is worth: this same strike landing unopposed. Against a guard that is the guard
			// it strips; against an open target it is the free hit. Objective — only the appetite is personal.
			float payoff = guard > 0f
				? DamageResolver.Resolve(strike, defence, 0f, combatSettings).ExpectedHealth / healthMax
				: health;

			// Stance progress is measured against what endurance they have LEFT, so a worn-down guard invites the break.
			float enduranceLeft = Mathf.Max(defender.StatHandler.ResourceStats.W.Value, 0.0001f);
			float stagger = Mathf.Clamp01(result.ExpectedEndurance / enduranceLeft) * payoff;

			return health + staggerWeight * stagger;
		}

		/// <summary>The target's combat component, cached until the target changes.</summary>
		private AgentCombatComponent DefenderOf(ITargetable target)
		{
			if (target == null)
			{
				return null;
			}
			if (!ReferenceEquals(target, cachedDefenderTarget))
			{
				cachedDefenderTarget = target;
				cachedDefender = target.Entity == null ? null : target.Entity.GetEntityComponent<AgentCombatComponent>();
			}
			return cachedDefender;
		}

		/// <summary>
		/// Affordability of <paramref name="cost"/> given it will actually drain <paramref name="amount"/> points —
		/// passed in rather than read off the authored number, since a perform cost is priced by the mass it swings.
		/// </summary>
		private float EvaluateCost(StatCost cost, float amount)
		{
			if (!cost.Required || string.IsNullOrEmpty(cost.Stat) || StatHandler == null)
			{
				return 1f;
			}

			float pool = Agent.Stats.GetStat(cost.Stat) ?? 0f;
			float unitCost = amount * (Agent.Stats.GetStat(cost.Stat.SubStat(AgentStatIdentifiers.SUB_DRAIN)) ?? 1f);

			if (unitCost <= 0f)
			{
				return 1f;
			}
			if (pool <= 0f)
			{
				return 0f;
			}

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
			AgentCombatComponent defender,
			float staggerWeight,
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

				// Value every follow-up against the target first; scoring is relative to the best sibling,
				// which keeps it on fu.Prio's ~1 scale at any level.
				followUpValues.Clear();
				float bestValue = 0f;
				for (int i = 0; i < current.FollowUps.Count; i++)
				{
					MoveFollowUp fu = current.FollowUps[i];
					float value = fu != null && fu.Move is ICombatMove combatMove
						? StrikeValue(combatMove, defender, staggerWeight)
						: 0f;
					followUpValues.Add(value);
					bestValue = Mathf.Max(bestValue, value);
				}

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
						score += bestValue > 0f ? followUpValues[i] / bestValue : 0f;

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
		/// How far an attack can close the gap by sticking: <see cref="CombatSettings.StickRange"/> scaled by the
		/// Agility-fed STICK_RANGE lane and cut by equip load. Also the lunge term of
		/// <see cref="ComputeEffectiveReach"/>, so the AI's fire gate can't drift from the swing's true reach.
		/// </summary>
		public float StickRange
		{
			get
			{
				if (combatSettings == null)
				{
					return 0f;
				}

				float lane = Agent.Stats.GetStat(AgentStatIdentifiers.STICK_RANGE) ?? 1f;
				float load = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD_PENALTY, true, 1f) ?? 1f;
				return combatSettings.StickRange * Mathf.Max(0f, lane) * load;
			}
		}

		/// <summary>
		/// <see cref="StickRange"/> shaped by the move's THRUST — Agility grants the budget, the move's geometry
		/// decides how much becomes distance. Forward half only: a withdrawing strike never lunges in.
		/// </summary>
		public float ComputeStickRange(ICombatMove move)
		{
			if (combatSettings == null || !(move is IMeleeCombatMove melee))
			{
				return 0f;
			}

			return StickRange * ThrustLane(melee);
		}

		/// <summary>
		/// <see cref="ComputeStickRange"/> at a 0-1 COMMIT: 0 is the free lunge every attack gets, 1 the bought maximum.
		/// </summary>
		public float ComputeStickRange(ICombatMove move, float commit)
		{
			return ComputeStickRange(move) * LungeFraction(commit);
		}

		/// <summary>
		/// Fraction of the full stick range a 0-1 <paramref name="commit"/> buys; below the free floor is never charged.
		/// </summary>
		public float LungeFraction(float commit)
		{
			float free = combatSettings == null ? 1f : Mathf.Clamp01(combatSettings.StickFreeFraction);
			return free + (1f - free) * Mathf.Clamp01(commit);
		}

		/// <summary>
		/// WIDEST angle (degrees) a lunge may turn in. 180 unburdened, so any turn completes; load lowers the
		/// ceiling and an aim beyond it is simply not reached.
		/// </summary>
		public float LungeTurnLimit
		{
			get
			{
				if (combatSettings == null)
				{
					return 0f;
				}

				float ratio = StatHandler != null ? StatHandler.LoadRatio : 0f;
				float burden = 1f + combatSettings.LungeTurnLoadFactor *
					Mathf.Pow(Mathf.Max(0f, ratio), combatSettings.LungeTurnLoadExponent);
				return FULL_TURN / Mathf.Max(0.0001f, burden);
			}
		}

		/// <summary>
		/// Stamina price of <paramref name="metres"/> of BOUGHT lunge; the free fraction is never passed in here.
		/// </summary>
		public float ComputeLungeCost(float metres)
		{
			return metres <= 0f ? 0f : metres * LungeCostPerMetre;
		}

		/// <summary>
		/// Inverse of <see cref="ComputeLungeCost"/>: the metres a spend actually bought, so a low bar shortens the leap.
		/// </summary>
		public float ComputeLungeMetres(float stamina)
		{
			float perMetre = LungeCostPerMetre;
			return perMetre <= 0f ? 0f : Mathf.Max(0f, stamina) / perMetre;
		}

		/// <summary>Stamina one bought metre costs this body: priced on mass, worsened by over-capacity load.</summary>
		private float LungeCostPerMetre
		{
			get
			{
				if (combatSettings == null)
				{
					return 0f;
				}

				float mass = Agent.Stats.GetStat(AgentStatIdentifiers.MASS, true, 1f) ?? 1f;
				float load = Agent.Stats.GetStat(AgentStatIdentifiers.LOAD_PENALTY, true, 1f) ?? 1f;
				return combatSettings.StickCostPerMetre *
					(Mathf.Max(0f, mass) / Mathf.Max(0.0001f, combatSettings.StickCostReferenceMass)) /
					Mathf.Max(0.01f, load);
			}
		}

		/// <summary>
		/// Fraction of a range knob the move's THRUST grants (<see cref="CombatSettings.StickRangeThrustScale"/>).
		/// Shared by the stick and the storm so a sweep closes reluctantly in both.
		/// </summary>
		private float ThrustLane(IMeleeCombatMove melee)
		{
			float thrust = Mathf.Clamp01(melee.StrikeDirection.z);
			return Mathf.Clamp01(Mathf.Lerp(
				combatSettings.StickRangeThrustScale.x, combatSettings.StickRangeThrustScale.y, thrust));
		}

		/// <summary>
		/// Extra distance a storm buys on top of the stick: the global <see cref="CombatSettings.StormRange"/>,
		/// thrust-laned and scaled by <see cref="StormCharge"/> — nothing until the deadzone clears, full at a
		/// pool-deep charge.
		/// </summary>
		public float ComputeStormRange(ICombatMove move, float chargeFraction)
		{
			if (combatSettings == null || !(move is IMeleeCombatMove melee))
			{
				return 0f;
			}

			float charge = StormCharge(chargeFraction);
			return charge <= 0f ? 0f : combatSettings.StormRange * ThrustLane(melee) * charge;
		}

		/// <summary>
		/// A charge as the STORM sees it: 0 until it clears StormMinCharge, then ramping to 1. Releasing a plain
		/// attack always banks a few points on the way out, and without this deadzone that counted as a storm.
		/// </summary>
		public float StormCharge(float chargeFraction)
		{
			return combatSettings == null
				? 0f
				: Mathf.InverseLerp(combatSettings.StormMinCharge, 1f, Mathf.Clamp01(chargeFraction));
		}

		/// <summary>
		/// Distance from the target's centre at which a stick aims to land: <see cref="CombatSettings.StickBite"/>
		/// deep into the move's OWN reach (the effective reach minus the lunge), floored at the attack-pose margin
		/// so it never aims into the body. Shared by the swing that performs the stick and the AI that times it.
		/// </summary>
		public float ComputeStickDesired(ICombatMove move, float targetRadius)
			=> ComputeBiteDistance(move, targetRadius, combatSettings == null ? 0f : combatSettings.StickBite);

		/// <summary>
		/// HARD floor on how close the attacking BODY may get. Same scale as <see cref="ComputeStickDesired"/> but off
		/// MeleeFloorBite, so landing point and depth limit tune separately.
		/// </summary>
		public float ComputeBodyFloor(ICombatMove move, float targetRadius)
			=> ComputeBiteDistance(move, targetRadius, combatSettings == null ? 1f : combatSettings.MeleeFloorBite);

		/// <summary>
		/// Distance from the target's centre for a <paramref name="bite"/> into the move's own reach (lunge excluded):
		/// 0 = tip of reach, 1 = bodies touching.
		/// </summary>
		private float ComputeBiteDistance(ICombatMove move, float targetRadius, float bite)
		{
			if (combatSettings == null)
			{
				return 0f;
			}

			float bodies = (Agent.Targetable != null ? Agent.Targetable.Radius : 0f) + targetRadius;
			float reach = ComputeEffectiveReach(move) - ComputeStickRange(move) + targetRadius;
			return Mathf.Max(bodies, Mathf.Lerp(reach, bodies, Mathf.Clamp01(bite)));
		}

		/// <summary>
		/// How long the stick will spend crossing the gap to a target <paramref name="distance"/> away before it
		/// arrives — the travel pause in <see cref="MeleeCombatBehaviourAsset"/>. Zero when already inside the
		/// bite. The AI's strike timing MUST include this: the swing no longer begins the instant the move
		/// commits, so without it the agent fires early against anything that is moving.
		/// A leap's airtime scales with √(gap / range) — <see cref="CombatSettings.StickTime"/> is the airtime
		/// of a FULL-range leap. A gap beyond <see cref="StickRange"/> produces no approach at all, so there is
		/// nothing to wait for.
		/// </summary>
		public float PredictStickTravelTime(ICombatMove move, float distance, float targetRadius)
		{
			if (combatSettings == null || !(move is IMeleeCombatMove))
			{
				return 0f;
			}

			float range = ComputeStickRange(move);
			float gap = distance - ComputeStickDesired(move, targetRadius);
			if (gap <= 0f || gap > range || range <= 0.01f)
			{
				return 0f;
			}

			return combatSettings.StickTime * Mathf.Sqrt(Mathf.Clamp01(gap / range));
		}

		/// <summary>
		/// Approximates the effective melee reach of a combat move *without* storm:
		/// global REACH + move range + acting-limb reach + stick range (for melee).
		/// Builds on <see cref="globalReach"/> (not <see cref="BaseReach"/>) so the acting limb is the only hand
		/// term — no double-count. Storm is handled by charge behaviours.
		/// </summary>
		public float ComputeEffectiveReach(ICombatMove move)
		{
			float reach = globalReach + move.Range;

			if (move is IMeleeCombatMove meleeMove)
			{
				float limbReach = Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(meleeMove.Limb)) ?? 0f;
				reach += limbReach + ComputeStickRange(meleeMove);
			}

			return reach;
		}

		// Reach used for range-fit scoring. Storm distance is only added in proportion to <paramref name="stormGate"/>
		// (the agent's built-up Static), so a move only counts as "engageable from afar" when it can actually
		// overcharge-dash. With stormGate = 0 this equals the storm-free effective reach.
		private float ComputeScoringReach(ICombatMove move, float stormGate)
		{
			return ComputeEffectiveReach(move) +
				ComputeStormRange(move, 1f) * Mathf.Clamp01(stormGate);
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
			/// <summary>Weapon distribution (x=NW/Slash, y=N/Power, z=NE/Pierce) * Coverage; (1,1,1) = fists.</summary>
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
			/// <summary>The weapon output this derives from — usage-resolved when armed.</summary>
			public readonly CombatOutput Weapon;
			/// <summary>Move sliders (x=Slash, y=Power, z=Pierce). Unarmed only; ignored when armed.</summary>
			public readonly Vector3 MoveSliders;
			/// <summary>UseArmament ? weapon⊙usageProfile : moveSliders (raw, pre-normalisation).</summary>
			public readonly Vector3 CombinedRaw;
			/// <summary>Normalised damage-type direction (×OutputScale for finishers).</summary>
			public readonly Vector3 Filter;
			/// <summary>Per-axis base output = BodyPhysics ⊙ Filter (pre charge/phase/strength).</summary>
			public readonly Vector3 Output;
			/// <summary>Body + weapon Power before the filter (×OutputScale); drives both flanks and carries blunt.</summary>
			public readonly float PowerBand;
			/// <summary>Body Power plus the weapon's Power lane × the filter's Power share (×OutputScale); feeds force.</summary>
			public readonly float ForceBand;

			public MoveOutput(CombatOutput weapon, Vector3 moveSliders, Vector3 combinedRaw, Vector3 filter, Vector3 output, float powerBand, float forceBand)
			{
				Weapon = weapon;
				MoveSliders = moveSliders;
				CombinedRaw = combinedRaw;
				Filter = filter;
				Output = output;
				PowerBand = powerBand;
				ForceBand = forceBand;
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

		// (NW, N, NE) * Coverage for an equipment data; fallback when none.
		private static Vector3 DistributionOf(IEquipmentData data, Vector3 fallback)
		{
			if (data == null)
			{
				return fallback;
			}
			Vector8 dis = data.PhysicsDistribution;
			return new Vector3(dis.NW, dis.N, dis.NE) * data.Coverage;
		}

		private static Vector3 DistributionOf(RuntimeEquipedData weapon, Vector3 fallback)
			=> DistributionOf(weapon == null ? null : weapon.EquipmentData, fallback);

		/// <summary>
		/// The fraction of a weapon's distribution that <paramref name="melee"/> actually realizes, by blending the
		/// weapon's <see cref="WeaponComponent.SwingProfile"/> and <see cref="WeaponComponent.ThrustProfile"/> by how
		/// much of the strike travels forward. USAGE, not orientation — a warpick thrust lands haft-first whichever
		/// way the spike happens to point mid-animation. Returns (1,1,1) when the weapon carries no profile, so an
		/// unconfigured weapon passes its distribution through untouched.
		/// </summary>
		private static Vector3 UsageProfile(RuntimeEquipedData weapon, IMeleeCombatMove melee)
		{
			WeaponComponent component = weapon == null ? null : weapon.Weapon;
			if (component == null)
			{
				return Vector3.one;
			}

			// A geometry-less strike has no forward travel to speak of, so it reads as a swing.
			Vector3 strike = melee.StrikeDirection;
			float magnitude = strike.magnitude;
			float thrustShare = magnitude > 0.0001f ? Mathf.Abs(strike.z) / magnitude : 0f;
			return Vector3.Lerp(component.SwingProfile, component.ThrustProfile, thrustShare);
		}

		/// <summary>
		/// The weapon's own Rank/Quality-derived physics as (x=Slash/NW, y=Power/N, z=Pierce/NE), written to its
		/// RuntimeData by <see cref="EquipmentInventoryBehaviour"/>. Zero when unarmed — weapons are not
		/// PhysicsPassive, so this is their active-use contribution.
		/// </summary>
		private Vector3 WeaponPhysics(RuntimeEquipedData weapon)
		{
			if (weapon == null || weapon.RuntimeItemData == null || StatHandler == null)
			{
				return Vector3.zero;
			}

			RuntimeDataCollection data = weapon.RuntimeItemData.RuntimeData;
			StatOctad ids = StatHandler.PhysicStats;
			return new Vector3(
				data.GetValue<float>(ids.northWest),
				data.GetValue<float>(ids.north),
				data.GetValue<float>(ids.northEast));
		}

		/// <summary>Total offensive physics (body + weapon) the distribution filter is applied to.</summary>
		private Vector3 OffensivePhysics(RuntimeEquipedData weapon) => BodyPhysics + WeaponPhysics(weapon);

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

		/// <summary>Surface of the armament <paramref name="move"/> swings; empty when it is unarmed.</summary>
		public string GetMoveSurface(ICombatMove move)
		{
			RuntimeEquipedData weapon = ResolveMoveWeapon(move);
			return weapon == null || weapon.EquipmentData == null ? null : weapon.EquipmentData.Surface;
		}

		/// <summary>
		/// Output of <paramref name="move"/> with a specific (possibly hypothetical) weapon. The single authority
		/// for what a strike delivers — both the hit pipeline and move selection read it.
		/// </summary>
		public MoveOutput GetMoveOutput(ICombatMove move, RuntimeEquipedData weapon)
		{
			Vector3 body = BodyPhysics;

			if (move is not IMeleeCombatMove melee)
			{
				// Non-melee combat move — neutral fallback; extension seam for future ranged/magic.
				Vector3 neutral = new Vector3(1f, 1f, 1f).normalized;
				return new MoveOutput(new CombatOutput(body, Vector3.one), Vector3.one, Vector3.one, neutral, body, body.y, body.y);
			}

			// Armed strikes swing the weapon's physics alongside the body's; unarmed moves are body-only.
			if (melee.UseArmament)
			{
				body = OffensivePhysics(weapon);
			}

			// Once an armament enters the equation the weapon alone decides which axes the strike fulfills:
			// its equipment distribution, taken down to the fraction this move's USAGE realizes. The move's own
			// sliders are the unarmed distribution and have no say here.
			Vector3 moveDist = new Vector3(melee.Slash, melee.Power, melee.Pierce);
			Vector3 weaponDist = melee.UseArmament
				? Vector3.Scale(DistributionOf(weapon, Vector3.zero), UsageProfile(weapon, melee))
				: Vector3.one;
			Vector3 combinedRaw = melee.UseArmament ? weaponDist : moveDist;
			float filterMag = combinedRaw.magnitude;
			Vector3 filter = filterMag > 0f ? combinedRaw / filterMag : Vector3.zero;

			// Finishers hit harder than the base stats allow, without changing what they hit WITH.
			filter *= melee.OutputScale;

			// Force takes the weapon's Power only in the share this strike swings it; the body is always behind it.
			float powerShare = filterMag > 0f ? combinedRaw.y / filterMag : 0f;
			float forceBand = melee.UseArmament ? BodyPhysics.y + WeaponPhysics(weapon).y * powerShare : body.y;

			Vector3 output = Vector3.Scale(body, filter);
			return new MoveOutput(new CombatOutput(body, weaponDist), moveDist, combinedRaw, filter, output,
				body.y * melee.OutputScale, forceBand * melee.OutputScale);
		}

		/// <summary>
		/// What a strike of <paramref name="move"/> would carry right now: no charge and no mid-swing phase, since
		/// neither exists before the swing. Malice is only knowable from the inside, so an observer never passes it.
		/// </summary>
		public StrikeData EstimateStrike(ICombatMove move, bool includeMalice = false)
		{
			MoveOutput output = GetMoveOutput(move);
			float slash = output.Slash;
			float power = output.Power;
			float pierce = output.Pierce;
			float malice = includeMalice ? ExpectedMaliceMultiplier(slash + power + pierce) : 1f;

			return new StrikeData(
				slash * malice,
				power * malice,
				pierce * malice,
				output.PowerBand * malice,
				output.ForceBand * malice,
				ComputeLimbMass(move),
				Agent.Body.RigidbodyWrapper.Mass,
				move is IMeleeCombatMove melee ? melee.BodyMassFraction : 0f,
				move is IMeleeCombatMove lifting ? lifting.StrikeDirection.y : 0f,
				Rank,
				luckStat ?? 0f,
				ContactSwingSpeed(move));
		}

		/// <summary>
		/// What one strike of <paramref name="move"/> from <paramref name="attacker"/> would do to THIS agent.
		/// Resolved guard-down: a threat read that fell when we raised our guard would argue us out of guarding.
		/// </summary>
		public DamageResult EstimateIncoming(AgentCombatComponent attacker, ICombatMove move)
		{
			return DamageResolver.Resolve(attacker.EstimateStrike(move), DefenceProfile, 0f, combatSettings);
		}

		/// <summary>
		/// The move an onlooker should brace for: the one in flight, else the one being planned, else the heaviest
		/// this agent could open with.
		/// </summary>
		public ICombatMove ThreatMove => CurrentCombatMove ?? PreferredMove ?? HeaviestMove;

		/// <summary>Heaviest opener in the moveset, refreshed on a slow timer — it only changes with equipment.</summary>
		private ICombatMove HeaviestMove
		{
			get
			{
				if (Time.time - heaviestMoveTime < HEAVIEST_MOVE_REFRESH)
				{
					return heaviestMove;
				}
				heaviestMoveTime = Time.time;
				heaviestMove = null;

				if (moveHandler == null || moveHandler.Moveset == null)
				{
					return null;
				}

				float best = -1f;
				foreach (KeyValuePair<string, IPerformanceMove> entry in moveHandler.Moveset)
				{
					if (entry.Value is not ICombatMove combatMove)
					{
						continue;
					}
					float magnitude = GetMoveOutput(combatMove).Magnitude;
					if (magnitude > best)
					{
						best = magnitude;
						heaviestMove = combatMove;
					}
				}
				return heaviestMove;
			}
		}

		/// <summary>Malice the pool could pay for on an offence of <paramref name="offence"/>: 1 + the covered share.</summary>
		private float ExpectedMaliceMultiplier(float offence)
		{
			if (offence <= 0f || StatHandler == null)
			{
				return 1f;
			}
			ResourceStat malice = StatHandler.ResourceStats.NW;
			return 1f + Mathf.Min(malice.Value, offence * malice.DrainMult) / offence;
		}

		#endregion Combat Output
	}
}
