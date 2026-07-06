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
		private EntityStat proofingStat;
		private EntityStat pliancyStat;
		private EntityStat protectionStat;
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
			proofingStat = agent.Stats.GetStat(AgentStatIdentifiers.PROOFING, true);
			pliancyStat = agent.Stats.GetStat(AgentStatIdentifiers.PLIANCY, true);
			protectionStat = agent.Stats.GetStat(AgentStatIdentifiers.PROTECTION, true);
			luckStat = agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);

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

			float coupling = SpaxFormulas.CalculateCoupling(hitData.Precision, pliancyStat);
			float critChance = SpaxFormulas.CalculateCritChance(coupling, vulnerability, hitData.Luck, luckStat);
			bool isCrit = !neglect &&
				hitData.Precision > 0f &&
				Random.value < critChance;

			float critDamage = isCrit
				? SpaxFormulas.CalculateDamage(hitData.Precision, pliancyStat)
				: 0f;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.COUPLING, coupling);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- 2. PIERCE LAYER ---
			float pierceDamage = 0f;
			float penetration = 0f;

			if (!neglect && hitData.Piercing > 0f)
			{
				// Proofing defends piercing; penetration = fraction that landed.
				pierceDamage = SpaxFormulas.CalculateDamage(hitData.Piercing, proofingStat);
				penetration = Mathf.Clamp01(pierceDamage / hitData.Piercing);
			}

			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);
			hitData.Data.SetValue(HitDataIdentifiers.PIERCING_DAMAGE, pierceDamage);

			// --- 3. BLUNT LAYER ---
			float impact = 0f;
			float bluntDamage = 0f;

			if (!neglect && hitData.Power > 0f)
			{
				// Power sits centre-octad, walled by proofing+pliancy; hardness, low penetration and clean coupling raise transfer.
				float bluntOffence = hitData.Power * (hardnessStat + (1f - penetration) + coupling);
				bluntDamage = SpaxFormulas.CalculateDamage(bluntOffence, proofingStat + pliancyStat);

				// Fraction of Power landed as blunt (0-1); reused for force, hit-pause and audio.
				impact = Mathf.Clamp01(bluntDamage / hitData.Power);
			}

			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);

			// --- 4. TOTAL PHYSICS DAMAGE ---
			float totalDamage = critDamage + pierceDamage + bluntDamage;
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// --- IMPACT & FORCE ---
			float force = hitData.Mass * hitData.Power * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			// Stun draws on sharp + crit + force; force carries the blunt (Mass x bluntDamage), counted once.
			float toEndure = neglect ? 0f : pierceDamage + critDamage + force;
			float enduranceDamage = statHandler.PointStats.W.Drain(
				toEndure,
				out bool stunned,
				out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			enduranceOverdraw *= toEndure / (enduranceDamage + enduranceOverdraw).Max(1f);
			float endured = toEndure > 0f ? (toEndure - enduranceOverdraw) / toEndure : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// --- STUN / IMPACT APPLICATION ---
			if (stunned)
			{
				agent.Actor.TryCancel(true);
				rigidbodyWrapper.ResetVelocity();
				rigidbodyWrapper.Push(hitData.Direction * force * endured.Invert(), 1f);
				stunHandler.EnterStun(hitData);
			}
			else if (neglect)
			{
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// --- INERTIA SHARING (clash) ---
			// Contact clash along the horizontal normal: closing momentum shared by mass, elasticity from Restitution. Landed hits only.
			if (!neglect)
			{
				Vector3 normal = (rigidbodyWrapper.Position - hitData.Hitter.Transform.position)
					.FlattenY().normalized;
				float closing = Mathf.Max(0f, Vector3.Dot(hitData.Inertia, normal));
				if (closing > 0f)
				{
					float totalMass = hitData.HitterMass + rigidbodyWrapper.Mass;
					float elasticity = 1f + combatSettings.Restitution;
					float receiverShare = hitData.HitterMass / totalMass * elasticity;
					float hitterShare = rigidbodyWrapper.Mass / totalMass * elasticity;

					rigidbodyWrapper.Push(normal * (closing * receiverShare));
					// Hitter's brake is applied on its side in ProcessHit.
					hitData.Data.SetValue(HitDataIdentifiers.INERTIA_BRAKE, -normal * (closing * hitterShare));
				}
			}

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				// Guard trades health for stance: blunt that would bleed health goes to endurance instead, scaled by guard weight. Pierce/crit untouched.
				float guardWeight = Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT));
				float healthDamage = Mathf.Max(0f, totalDamage - bluntDamage * guardWeight);

				// Grace absorbs only the mortal overflow, leaving at least 1 HP while it lasts.
				float mortal = healthDamage - Mathf.Max(0f, statHandler.PointStats.SW.Value - 1f);
				if (mortal > 0f)
				{
					float drained = statHandler.PointStats.SE.Drain(mortal);
					healthDamage -= drained;
					hitData.Data.SetValue(HitDataIdentifiers.GRACE, drained);
				}

				float damageDealt = statHandler.PointStats.SW.Drain(healthDamage, out bool dead, out _);
				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT, damageDealt);

				// --- MALICE BUILDUP ---
				// Only builds on actual HP lost.
				if (damageDealt > 0f &&
					hitData.Hitter != null &&
					hitData.Hitter is IAgent)
				{
					statHandler.PointStats.NW.Gain(damageDealt);
				}

				if (dead)
				{
					stunHandler.EnterStun(hitData, 5f);
					DeathContext context = new DeathContext(agent, hitData.Hitter, "Hit");
					agent.Die(context);
				}
			}

			// Build Static (NE) for defending: parry/deflect full, block half, partial guard scaled. Threat = potential force (Mass x Power).
			float staticThreat = hitData.Mass * hitData.Power * combatSettings.StaticGain;
			if (parried || deflected)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat;
			}
			else if (blocked)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat * 0.5f;
			}
			else
			{
				float guardWeight = hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT);
				if (guardWeight > 0f)
				{
					statHandler.PointStats.NE.Current.BaseValue += staticThreat * 0.5f * Mathf.Clamp01(guardWeight);
				}
			}

			// --- HIT PAUSE ---
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.HitPauseReceiver.Lerp(impact) * endured.InvertClamped()),
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

			if (debug)
			{
				SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString() + "\nEntity Stats:\n" + Entity.Stats.GetSnapshot());
			}
		}
	}
}
