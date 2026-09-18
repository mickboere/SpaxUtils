using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling hits coming through in the IHittable component.
	/// </summary>
	public class AgentHitHandlerComponent : EntityComponentMono
	{
		public IReadOnlyDictionary<string, float> DamageLedger => damageLedger;

		public bool Invulnerable => agent.RuntimeData.GetValue(AgentDataIdentifiers.INVULNERABLE, false);

		/// <summary>Seconds until this agent's current hit-pause lifts; 0 when none is running.</summary>
		public float HitPauseRemaining => hitPauseMod == null ? 0f : Mathf.Max(0f, hitPauseMod.Timer.Remaining);

		/// <summary>Current value of the hit-pause curve (the timescale it applies); 1 when no pause is running.</summary>
		public float HitPauseCurveValue => hitPauseMod == null || hitPauseMod.Timer.Expired
			? 1f
			: combatSettings.HitPauseCurve.Evaluate(hitPauseMod.Timer.Progress);

		[SerializeField, Tooltip("When enabled, hits landing toward this agent's back raise effective Vulnerability toward 1 (shaped by CombatSettings.RearExposureCurve), letting crits land from behind even through guard. When disabled, only the base Vulnerability stat is used regardless of hit angle.")]
		private bool backTurnWeakness = false;

		[SerializeField] private bool debug;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;
		private AgentStatHandler statHandler;

		private EntityStat timescaleStat;
		private EntityStat hardnessStat;
		private EntityStat vulnerabilityStat;
		private EntityStat armorStat;
		private EntityStat yieldStat;
		private EntityStat wardStat;
		private EntityStat luckStat;

		private TimedCurveModifier hitPauseMod;

		private Dictionary<string, float> damageLedger = new Dictionary<string, float>();

		public void InjectDependencies(
			IAgent agent,
			IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper,
			CombatSettings combatSettings,
			CallbackService callbackService,
			IStunHandler stunHandler,
			AgentStatHandler statHandler)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
			this.stunHandler = stunHandler;
			this.statHandler = statHandler;
		}

		protected void OnEnable()
		{
			timescaleStat = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true);
			hardnessStat = agent.Stats.GetStat(AgentStatIdentifiers.HARDNESS, true);
			vulnerabilityStat = agent.Stats.GetStat(AgentStatIdentifiers.VULNERABILITY, true);
			armorStat = agent.Stats.GetStat(AgentStatIdentifiers.ARMOR, true);
			yieldStat = agent.Stats.GetStat(AgentStatIdentifiers.YIELD, true);
			wardStat = agent.Stats.GetStat(AgentStatIdentifiers.WARD, true);
			luckStat = agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);

			hittable.Subscribe(this, OnHitEvent, -100);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
		}

		private void OnHitEvent(HitData hitData)
		{
			bool blocked = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);
			bool parried = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED);
			bool neglect = blocked;

			// A parry resolves as a real hit; its timing quality decides how much of it endurance pays.
			float parryQuality = parried ? Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.PARRY_QUALITY)) : 0f;

			// --- 1. GUARD ---
			// Rear exposure lifts Vulnerability toward 1 and takes the guard off that hit.
			float guardWeight = Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT));
			float vulnerability = vulnerabilityStat.Value;
			float frontal = 1f;
			if (backTurnWeakness)
			{
				Vector3 toHitter = (hitData.Hitter.Transform.position - rigidbodyWrapper.Position).FlattenY().normalized;
				float rearParam = toHitter.NormalizedDot(rigidbodyWrapper.Forward).Invert();
				float rearExposure = Mathf.Clamp01(combatSettings.RearExposureCurve.Evaluate(rearParam));
				vulnerability = Mathf.Lerp(vulnerability, 1f, rearExposure);
				frontal = 1f - rearExposure;
			}
			float guard = guardWeight * frontal;

			// --- 2. RESOLVE ---
			// THE pipeline, shared with the AI's estimates; only the crit roll is the hit's own.
			StrikeData strike = new StrikeData(hitData);
			DefenceData defence = new DefenceData(armorStat, yieldStat, hardnessStat, vulnerability, luckStat);
			DamageResult result = DamageResolver.Resolve(strike, defence, guard, combatSettings, neglect);

			// A parry catches the point before it can find a gap, so it never crits.
			bool isCrit = !parried && result.CritChance > 0f && Random.value < result.CritChance;
			float critDamage = isCrit ? result.CritDamage : 0f;
			float slashDamage = result.Slash;
			float pierceDamage = result.Pierce;
			float bluntDamage = result.Blunt;
			float impact = result.Impact;
			float force = result.Force;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.COUPLING, result.Coupling);
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, result.Penetration);
			hitData.Data.SetValue(HitDataIdentifiers.SLASH_DAMAGE, slashDamage);
			hitData.Data.SetValue(HitDataIdentifiers.PIERCE_DAMAGE, pierceDamage);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);
			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- 3. TOTAL PHYSICS DAMAGE ---
			float totalDamage = result.TotalWith(critDamage);
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// The hitter measures its output against this; non-agent hittables report nothing and pay no EXP.
			float healthMax = statHandler.ResourceStats.SW.Max;
			hitData.Data.SetValue(HitDataIdentifiers.HEALTH_MAX, healthMax);

			// --- ENDURANCE DAMAGE ---
			// Damage wears endurance at post-guard rates; force is centre-octad like blunt, so walled by the mean.
			float full = result.EnduranceWith(critDamage);

			// Parry timing splits the cost: we pay what it missed, the hitter what it caught (applied their side).
			float toEndure = neglect ? 0f : full * (1f - parryQuality);
			if (parried)
			{
				hitData.Data.SetValue(HitDataIdentifiers.ENDURANCE_RETURN, full * parryQuality);
			}

			float enduranceDamage = statHandler.ResourceStats.W.Drain(
				toEndure,
				out bool stunned,
				out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			enduranceOverdraw *= toEndure / (enduranceDamage + enduranceOverdraw).Max(1f);
			float endured = toEndure > 0f ? (toEndure - enduranceOverdraw) / toEndure : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// --- STUN ---
			// No force of its own; the extra travel comes from zeroing Control, not a second push.
			if (stunned)
			{
				agent.Actor.TryCancel(true);
				rigidbodyWrapper.ResetVelocity();
				stunHandler.EnterStun(hitData);
			}

			// --- KNOCKBACK ---
			// CLASH = the collision, FORCE = the strike. Both scale by impact and by the target's footing.
			Vector3 normal = (rigidbodyWrapper.Position - hitData.Hitter.Transform.position)
				.FlattenY().normalized;

			// Strike direction, so an uppercut launches; geometry-less moves fall back to the normal.
			Vector3 push = hitData.Direction.sqrMagnitude > 0.0001f
				? hitData.Direction.normalized
				: normal;

			float elasticity = 1f + combatSettings.Restitution;

			// Recoverable, not Max — the reserve is the ceiling they can fight back up to. Post-drain.
			float spent = statHandler.ResourceStats.W.PercentageRecoverable.InvertClamped();
			float footing = 1f - spent;

			// Closing speed less our own outbound share — full relative speed would double-count a mutual clash.
			float receiverOut = Mathf.Max(0f, Vector3.Dot(rigidbodyWrapper.PredictedVelocity, normal));
			float closing = Mathf.Max(0f, Vector3.Dot(hitData.Inertia, normal) - receiverOut) * impact;
			float totalMass = hitData.HitterMass + rigidbodyWrapper.Mass;
			Vector3 clashPush = totalMass > 0f
				? normal * (closing * Mathf.Lerp(1f, hitData.HitterMass / totalMass, footing) * elasticity)
				: Vector3.zero;
			Vector3 clashBrake = totalMass > 0f
				? -normal * (closing * (rigidbodyWrapper.Mass / totalMass) * footing * elasticity)
				: Vector3.zero;

			// FORCE — an even split while they hold their stance, all theirs when spent.
			// A negated hit turns the whole strike back on the attacker; a parry does so by its quality.
			float receiverShare = neglect ? 0f : Mathf.Lerp(Mathf.Lerp(0.5f, 1f, spent), 0f, parryQuality);
			float impulse = force * elasticity;

			rigidbodyWrapper.Push(clashPush + push * (impulse * receiverShare / rigidbodyWrapper.Mass));

			// Hitter's half is applied on its side in ProcessHit.
			hitData.Data.SetValue(HitDataIdentifiers.INERTIA_BRAKE, hitData.HitterMass > 0f
				? clashBrake - push * (impulse * (1f - receiverShare) / hitData.HitterMass)
				: clashBrake);

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				// Guard trades health for stance: blunt is cancelled off health by guard weight, and already rides endurance
				// as force. Edges and points were converted upstream; only a crit strikes through.
				float guarded = bluntDamage * guard;
				// A parry holds everything for as long as endurance pays for it.
				float healthDamage = parried ? 0f : Mathf.Max(0f, totalDamage - guarded);

				// What this hit would have done with no guard in the way — the total the Static ledger splits.
				bool held = (guard > 0f || parried) && !neglect;
				float unguarded = held
					? DamageResolver.Resolve(strike, defence, 0f, combatSettings).TotalWith(critDamage)
					: totalDamage;

				// A broken guard or parry only held the share endurance paid for; the rest lands as if unguarded.
				if (stunned && held)
				{
					healthDamage += Mathf.Max(0f, unguarded - healthDamage) * (1f - endured);
					guarded *= endured;
				}

				// EARTH: the damage the guard cancelled, measured against our own health.
				if (guarded > 0f && healthMax > 0f)
				{
					statHandler.RewardExp(Element.Earth, guarded / healthMax, ExpSources.GUARDED_DAMAGE);
				}

				// Grace absorbs only the mortal overflow, leaving at least 1 HP while it lasts.
				float mortal = healthDamage - Mathf.Max(0f, statHandler.ResourceStats.SW.Value - 1f);
				if (mortal > 0f)
				{
					float drained = statHandler.ResourceStats.SE.Drain(mortal);
					healthDamage -= drained;
					hitData.Data.SetValue(HitDataIdentifiers.GRACE, drained);
				}

				float dealt = statHandler.ResourceStats.SW.Drain(healthDamage, out bool dead, out _);
				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT, dealt);

				// --- MALICE + STATIC LEDGER ---
				// The damage this hit carried splits in two and nothing is lost: what got THROUGH is the
				// hitter's Static (banked their side), what we STOPPED is ours. Spite answers the offence.
				float stopped = Mathf.Max(0f, unguarded - dealt);
				if (stopped > 0f)
				{
					statHandler.ResourceStats.NE.Current.BaseValue += stopped * combatSettings.StaticPerDamage;

					// LIGHT: a parry pays for the threat it neutralised.
					if (parried)
					{
						statHandler.RewardExpPoints(Element.Light, stopped, ExpSources.PARRY);
					}
				}

				if (hitData.Hitter != null && hitData.Hitter is IAgent)
				{
					float incomingOffence = hitData.Slash + hitData.Power + hitData.Pierce;
					if (incomingOffence > 0f)
					{
						statHandler.ResourceStats.NW.Gain(incomingOffence * combatSettings.MaliceGain);
					}
				}

				if (dead)
				{
					hitData.Data.SetValue(HitDataIdentifiers.KILLED, true);
					stunHandler.EnterStun(hitData, 5f);
					DeathContext context = new DeathContext(agent, hitData.Hitter, "Hit");
					agent.Die(context);
				}
			}

			// --- HIT PAUSE ---
			// Crits pause for a fixed beat, parries earn their advantage by quality; the rest scales with impact.
			float pauseTime = parried ? Mathf.Lerp(combatSettings.ParriedHitPause, combatSettings.ParrierHitPause, parryQuality)
				: isCrit ? combatSettings.CritReceiverHitPause
				: combatSettings.HitPauseReceiver.Lerp(impact);

			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(pauseTime),
				callbackService);

			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Update ledger.
			if (damageLedger.ContainsKey(hitData.Hitter.ID))
			{
				damageLedger[hitData.Hitter.ID] += totalDamage;
			}
			else
			{
				damageLedger[hitData.Hitter.ID] = totalDamage;
			}

			// Damage balance instrumentation; uncomment and set Debuddy's filter to "DMGTEST" to capture fights.
			//SpaxDebug.Log($"[DMGTEST]", hitData.ToString() +
			//	$"\nDefence: Armor={armorStat.Value:F1}, Yield={yieldStat.Value:F1}, Hardness={hardnessStat.Value:F2}, Ward={wardStat.Value:F1}, Vulnerability={vulnerabilityStat.Value:F2}" +
			//	$"\nHealth(SW)={statHandler.ResourceStats.SW.Value:F1}/{statHandler.ResourceStats.SW.Max.Value:F1}" +
			//	$"\nEndurance(W)={statHandler.ResourceStats.W.Value:F1}/{statHandler.ResourceStats.W.Max.Value:F1}" +
			//	$"\nBodyLevels={statHandler.BodyLevels.Vector8.ToStringShort()}" +
			//	$"\nPhysics={statHandler.PhysicStats.Vector8.ToStringShort()}");

			if (debug)
			{
				SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString() + "\nEntity Stats:\n" + Entity.Stats.GetSnapshot());
			}
		}
	}
}
