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
			bool deflected = hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);
			bool neglect = blocked || parried || deflected;

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

			// Guard (Earth) flattens edges: the full Armor, shield included, comes off the Slash.
			// Points only run the wall twice and never reach zero; dashing (Air) is their real counter.
			float slashIn = neglect ? 0f : Mathf.Max(0f, hitData.Slash - armorStat * guard);
			float couplingOpen = SpaxFormulas.CalculateCoupling(hitData.Pierce, yieldStat, combatSettings.CritPivot);
			float pierceIn = Mathf.Lerp(hitData.Pierce, hitData.Pierce * couplingOpen, guard);

			// --- 2. CONTESTS ---
			// Edge vs Armor, point vs Yield, on what guard let through; whatever neither takes lands as blunt below.
			float band = hitData.PowerBand;
			float penetration = SpaxFormulas.Transfer(slashIn, armorStat);
			float coupling = SpaxFormulas.CalculateCoupling(pierceIn, yieldStat, combatSettings.CritPivot);

			// Each flank needs the power band behind it to get past the OTHER wall.
			float slashDrive = SpaxFormulas.Transfer(band, yieldStat);
			float pierceDrive = SpaxFormulas.Transfer(band, armorStat);

			// --- 3. SLASH ---
			float slashDamage = slashIn * SpaxFormulas.Contests(penetration, slashDrive, combatSettings.ContestPower);

			// --- 4. PIERCE & CRIT ---
			// A crit found a gap: its odds come from the guarded point, but it lands with the unguarded one.
			float pierceOpen = neglect ? 0f : hitData.Pierce * SpaxFormulas.Contests(couplingOpen, pierceDrive, combatSettings.ContestPower);
			float pierceDamage = neglect ? 0f : pierceIn * SpaxFormulas.Contests(coupling, pierceDrive, combatSettings.ContestPower);
			bool isCrit = !neglect &&
				hitData.Pierce > 0f &&
				Random.value < SpaxFormulas.CalculateCritChance(coupling, vulnerability, hitData.Luck, luckStat);
			float critDamage = isCrit ? pierceOpen * combatSettings.CritMultiplier : 0f;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.COUPLING, coupling);
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);
			hitData.Data.SetValue(HitDataIdentifiers.SLASH_DAMAGE, slashDamage);
			hitData.Data.SetValue(HitDataIdentifiers.PIERCE_DAMAGE, pierceDamage);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- 5. BLUNT ---
			// What neither cut nor caught, carried by the whole power band. Centre-octad, so walled by the mean.
			float meanDefence = (armorStat + yieldStat) * 0.5f;
			float Blunt(float edge, float point, out float effectiveness, out float wall)
			{
				effectiveness = Mathf.Sqrt(Mathf.Clamp01(hardnessStat) * Mathf.Clamp01((1f - edge) * (1f - point)));
				float offence = band * effectiveness * combatSettings.BluntScale;
				wall = SpaxFormulas.Transfer(offence, meanDefence, combatSettings.BluntWallExponent);
				return offence * SpaxFormulas.Contests(wall, SpaxFormulas.Transfer(band, meanDefence), combatSettings.ContestPower);
			}
			float blunt = Blunt(penetration, coupling, out float bluntEffectiveness, out float bluntWall);
			float bluntDamage = neglect ? 0f : blunt;

			// Momentum TRANSMITTED: what the own wall refused made the contact rigid, and guard stiffens the rest.
			// A failed drive didn't deliver, so it's excluded.
			float rigidity = 1f - (1f - guard) * bluntWall;
			float impact = Mathf.Lerp(bluntEffectiveness, 1f, rigidity);

			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);

			// --- 6. TOTAL PHYSICS DAMAGE ---
			float totalDamage = slashDamage + pierceDamage + critDamage + bluntDamage;
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// The hitter measures its output against this; non-agent hittables report nothing and pay no EXP.
			float healthMax = statHandler.PointStats.SW.Max;
			hitData.Data.SetValue(HitDataIdentifiers.HEALTH_MAX, healthMax);

			// --- IMPACT & FORCE ---
			// The force band, scaled by mass as ratios so a heavy club outpushes a light one at any level.
			float force = hitData.ForceBand * combatSettings.ForceMassFactor(hitData.LimbMass, hitData.HitterMass, hitData.BodyMassFraction) * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			// Damage wears endurance at post-guard rates; force is centre-octad like blunt, so walled by the mean.
			float stagger = SpaxFormulas.CalculateDamage(force, meanDefence);
			float full = combatSettings.StaggerDamageWeight * (slashDamage + pierceDamage + critDamage) + stagger;

			// A deflect splits what it negated: we eat our share, the hitter eats the rest (applied their side).
			float toEndure = deflected ? full * combatSettings.DeflectEnduranceShare : (neglect ? 0f : full);
			if (deflected)
			{
				hitData.Data.SetValue(HitDataIdentifiers.ENDURANCE_RETURN, full - toEndure);
			}

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

			// FORCE — an even split while they hold their stance, all theirs when spent.
			// A negated hit withstands the strike and turns the whole of it back on the attacker.
			float receiverShare = neglect ? 0f : Mathf.Lerp(0.5f, 1f, spent);
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
				float healthDamage = Mathf.Max(0f, totalDamage - guarded);

				// A broken guard only held the share endurance paid for; the rest lands as if unguarded.
				if (stunned && guard > 0f && !neglect)
				{
					float openPenetration = SpaxFormulas.Transfer(hitData.Slash, armorStat);
					float unguarded = hitData.Slash * SpaxFormulas.Contests(openPenetration, slashDrive, combatSettings.ContestPower) + pierceOpen + critDamage
						+ Blunt(openPenetration, couplingOpen, out _, out _);
					healthDamage += Mathf.Max(0f, unguarded - healthDamage) * (1f - endured);
					guarded *= endured;
				}

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
			float staticThreat = hitData.StrikeMass * hitData.Power * combatSettings.StaticGain;
			if (parried || deflected)
			{
				float built = staticThreat * combatSettings.DeflectStaticPercent;
				statHandler.PointStats.NE.Current.BaseValue += built;

				// LIGHT: a deflect pays for the threat it neutralised, measured in the Static it grounded.
				statHandler.RewardExpPoints(Element.Light, built, ExpSources.DEFLECT);
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
			// Deflects and crits pause for a fixed beat; everything else scales with impact.
			float pauseTime = deflected ? combatSettings.DeflectorHitPause
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
