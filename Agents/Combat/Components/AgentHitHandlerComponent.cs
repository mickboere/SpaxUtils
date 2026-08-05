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
		private EntityStat guardStat;

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
			guardStat = agent.Stats.GetStat(AgentStatIdentifiers.GUARD, true);

			hittable.Subscribe(this, OnHitEvent, 100);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
		}

		private void OnHitEvent(HitData hitData)
		{
			bool blocked = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);
			bool parried = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED);
			bool deflected = hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);
			bool neglect = blocked || parried || deflected;

			// --- 1. CRIT LAYER ---
			// Rear exposure: hits toward the back lerp Vulnerability up toward 1 (guard only lowers the base lerped from).
			float vulnerability = vulnerabilityStat.Value;
			if (backTurnWeakness)
			{
				Vector3 toHitter = (hitData.Hitter.Transform.position - rigidbodyWrapper.Position).FlattenY().normalized;
				float rearParam = toHitter.NormalizedDot(rigidbodyWrapper.Forward).Invert();
				float rearExposure = Mathf.Clamp01(combatSettings.RearExposureCurve.Evaluate(rearParam));
				vulnerability = Mathf.Lerp(vulnerability, 1f, rearExposure);
			}

			float coupling = SpaxFormulas.CalculateCoupling(hitData.Pierce, yieldStat);
			bool isCrit = !neglect &&
				hitData.Pierce > 0f &&
				Random.value < SpaxFormulas.CalculateCritChance(coupling, vulnerability, hitData.Luck, luckStat);

			float critDamage = isCrit
				? SpaxFormulas.CalculateDamage(hitData.Pierce, yieldStat)
				: 0f;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.COUPLING, coupling);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- 2. SLASH LAYER ---
			float slashDamage = 0f;
			float penetration = 0f;

			if (!neglect && hitData.Slash > 0f)
			{
				// Armor defends slashing; penetration = fraction that landed.
				slashDamage = SpaxFormulas.CalculateDamage(hitData.Slash, armorStat);
				penetration = Mathf.Clamp01(slashDamage / hitData.Slash);
			}

			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);
			hitData.Data.SetValue(HitDataIdentifiers.SLASH_DAMAGE, slashDamage);

			// --- 3. BLUNT LAYER ---
			// What neither cut nor caught; either one high lets the strike pass through instead of transmitting.
			float bluntness = Mathf.Clamp01((1f - penetration) * (1f - coupling));
			float bluntEffectiveness = Mathf.Sqrt(Mathf.Clamp01(hardnessStat) * bluntness);

			// Centre-octad, so walled by the MEAN of its two flanking defences, not their sum.
			float bluntOffence = hitData.Power * bluntEffectiveness;
			float bluntDamage = neglect
				? 0f
				: SpaxFormulas.CalculateDamage(bluntOffence, (armorStat + yieldStat) * 0.5f);

			// Momentum TRANSMITTED, not damage dealt: what the defence refused is what made the contact rigid.
			float guardWeight = Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT));
			float rigidity = bluntOffence > 0f
				? Mathf.Max(guardWeight, Mathf.Clamp01(1f - bluntDamage / bluntOffence))
				: guardWeight;
			float impact = Mathf.Lerp(bluntEffectiveness, 1f, rigidity);

			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);

			// --- 4. TOTAL PHYSICS DAMAGE ---
			float totalDamage = critDamage + slashDamage + bluntDamage;
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// The hitter measures its output against this; non-agent hittables report nothing and pay no EXP.
			float healthMax = statHandler.PointStats.SW.Max;
			hitData.Data.SetValue(HitDataIdentifiers.HEALTH_MAX, healthMax);

			// --- IMPACT & FORCE ---
			// Mass and Power ADD so neither zeroes the other; impact is the fraction that transmits.
			float force = (hitData.Mass + hitData.Power) * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			// Endurance is Earth's pool, so Earth's physic walls it. Only force is raw; slash/crit are already mitigated.
			// Bracing absorbs what is left, clamped to x1 so a weak guard never amplifies it.
			float stagger = SpaxFormulas.CalculateDamage(force, armorStat);
			float toEndure = neglect ? 0f : (slashDamage + critDamage + stagger) / (guardStat.Value * guardWeight).Max(1f);
			float enduranceDamage = statHandler.PointStats.W.Drain(
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
			else if (neglect)
			{
				// Perfectly negated (block/parry/deflect): the attacker eats the stun, we just take the shunt.
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// --- KNOCKBACK ---
			// CLASH = the collision, FORCE = the strike. Both scale by impact and by the target's footing.
			if (!neglect)
			{
				Vector3 normal = (rigidbodyWrapper.Position - hitData.Hitter.Transform.position)
					.FlattenY().normalized;

				// Strike direction, so an uppercut launches; geometry-less moves fall back to the normal.
				Vector3 push = hitData.Direction.sqrMagnitude > 0.0001f
					? hitData.Direction.normalized
					: normal;

				float elasticity = 1f + combatSettings.Restitution;

				// Recoverable, not Max — the reserve is the ceiling they can fight back up to. Post-drain.
				float spent = statHandler.PointStats.W.PercentageRecoverable.InvertClamped();
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

				// FORCE — same footing rule: an even split while they can hold their stance, all theirs when spent.
				float receiverShare = Mathf.Lerp(0.5f, 1f, spent);
				float impulse = force * elasticity;

				rigidbodyWrapper.Push(clashPush + push * (impulse * receiverShare / rigidbodyWrapper.Mass));

				// Hitter's half is applied on its side in ProcessHit.
				hitData.Data.SetValue(HitDataIdentifiers.INERTIA_BRAKE, hitData.HitterMass > 0f
					? clashBrake - push * (impulse * (1f - receiverShare) / hitData.HitterMass)
					: clashBrake);
			}

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				// Guard trades health for stance: blunt is cancelled off health by guard weight. The endurance hit above already
				// carries that blunt as force, so guard pays for it there instead (already divided by GUARD above). Pierce/crit untouched.
				float guarded = bluntDamage * guardWeight;
				float healthDamage = Mathf.Max(0f, totalDamage - guarded);

				// EARTH: the damage the guard cancelled, measured against our own health.
				if (guarded > 0f && healthMax > 0f)
				{
					statHandler.RewardExp(Element.Earth, guarded / healthMax, ExpSources.GUARDED_DAMAGE);
				}

				// Grace absorbs only the mortal overflow, leaving at least 1 HP while it lasts.
				float mortal = healthDamage - Mathf.Max(0f, statHandler.PointStats.SW.Value - 1f);
				if (mortal > 0f)
				{
					float drained = statHandler.PointStats.SE.Drain(mortal);
					healthDamage -= drained;
					hitData.Data.SetValue(HitDataIdentifiers.GRACE, drained);
				}

				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT,
					statHandler.PointStats.SW.Drain(healthDamage, out bool dead, out _));

				// --- MALICE BUILDUP ---
				// Spite answers the offence aimed at us, not the wound it left; a fully guarded hit builds the same as a clean one.
				// Basis matches what Malice is spent against (MeleeCombatBehaviourAsset), keeping the ledger symmetric.
				if (hitData.Hitter != null && hitData.Hitter is IAgent)
				{
					float incomingOffence = hitData.Slash + hitData.Power + hitData.Pierce;
					if (incomingOffence > 0f)
					{
						statHandler.PointStats.NW.Gain(incomingOffence * combatSettings.MaliceGain);
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

			// Build Static (NE) for defending. Threat = potential force (Mass × Power); each outcome takes its own fraction (partial guard scales further by guard weight).
			float staticThreat = hitData.Mass * hitData.Power * combatSettings.StaticGain;
			if (parried || deflected)
			{
				float built = staticThreat * combatSettings.DeflectStaticPercent;
				statHandler.PointStats.NE.Current.BaseValue += built;

				// LIGHT: a parry pays for the threat it neutralised, measured in the Static it grounded.
				statHandler.RewardExpPoints(Element.Light, built, ExpSources.PARRY);
			}
			else if (blocked)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat * combatSettings.BlockStaticPercent;
			}
			else if (guardWeight > 0f)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat * combatSettings.BlockStaticPercent * guardWeight;
			}

			// --- HIT PAUSE ---
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.HitPauseReceiver.Lerp(impact)),
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
			//	$"\nHealth(SW)={statHandler.PointStats.SW.Value:F1}/{statHandler.PointStats.SW.Max.Value:F1}" +
			//	$"\nEndurance(W)={statHandler.PointStats.W.Value:F1}/{statHandler.PointStats.W.Max.Value:F1}" +
			//	$"\nBodyLevels={statHandler.BodyLevels.Vector8.ToStringShort()}" +
			//	$"\nPhysics={statHandler.Physics.Vector8.ToStringShort()}");

			if (debug)
			{
				SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString() + "\nEntity Stats:\n" + Entity.Stats.GetSnapshot());
			}
		}
	}
}
