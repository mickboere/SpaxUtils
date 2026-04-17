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
			float coupling = SpaxFormulas.CalculateCoupling(hitData.Precision, pliancyStat);
			float critChance = SpaxFormulas.CalculateCritChance(coupling, vulnerabilityStat, hitData.Luck, luckStat);
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
				// Proofing defends against Piercing.
				pierceDamage = SpaxFormulas.CalculateDamage(hitData.Piercing, proofingStat);

				// Penetration is defined by how much of the incoming piercing becomes actual piercing damage.
				penetration = Mathf.Clamp01(pierceDamage / hitData.Piercing);
			}

			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);
			hitData.Data.SetValue(HitDataIdentifiers.PIERCING_DAMAGE, pierceDamage);

			// --- 3. BLUNT LAYER ---
			float impact = 0f;
			float bluntDamage = 0f;

			if (!neglect && hitData.Power > 0f)
			{
				// Impact is defined only by Coupling and Penetration.
				impact = coupling * (1f - penetration) * 2f;

				// Power is not defended; Impact determines how much Power couples into blunt damage.
				float bluntOffence = hitData.Power * impact;
				bluntDamage = SpaxFormulas.CalculateDamage(bluntOffence, (proofingStat + pliancyStat) * 0.5f);
			}

			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);

			// --- 4. TOTAL PHYSICS DAMAGE ---
			float totalDamage = critDamage + pierceDamage + bluntDamage;

			// --- 5. GRACE INTERVENTION ---
			// Applied AFTER physics calculation. It absorbs damage, it does not act as armor.
			if (totalDamage > 0f)
			{
				// Drain Grace
				float drained = statHandler.PointStats.SE.Drain(totalDamage);
				hitData.Data.SetValue(HitDataIdentifiers.GRACE, drained);

				// Reduce final damage by amount successfully drained from Grace
				totalDamage -= drained;
			}

			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// --- IMPACT & FORCE ---
			float force = hitData.Mass * hitData.Power * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			float toEndure = neglect ? 0f : totalDamage + force;
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

			// Mandatory inertia transfer.
			rigidbodyWrapper.Push(
				hitData.Inertia * (endured.Invert() * 0.5f),
				hitData.HitterMass);

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				float damageDealt = statHandler.PointStats.SW.Drain(totalDamage, out bool dead, out _);
				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT, damageDealt);

				// --- MALICE BUILDUP ---
				if (damageDealt > 0f &&
					hitData.Hitter != null &&
					hitData.Hitter is IAgent)
				{
					// Only builds if HP was actually lost (Grace prevents Malice gain)
					statHandler.PointStats.NW.Gain(damageDealt);
				}

				if (dead)
				{
					stunHandler.EnterStun(hitData, 5f);
					DeathContext context = new DeathContext(agent, hitData.Hitter, "Hit");
					agent.Die(context);
				}
			}

			// Build Static
			statHandler.PointStats.NE.Current.BaseValue += endured * force * 0.1f;

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

			//SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString() + "\nEntity Stats:\n" + Entity.Stats.GetSnapshot());
		}
	}
}
