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
		protected bool Invulnerable => agent.RuntimeData.GetValue(AgentDataIdentifiers.INVULNERABLE, false);

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;
		private AgentStatHandler statHandler;

		private EntityStat timescaleStat;
		private EntityStat poiseStat;       // Maps to 'defence' in the formula
		private EntityStat protectionStat;  // Maps to 'protection' in the formula
		private EntityStat luckStat;

		private TimedCurveModifier hitPauseMod;

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
			poiseStat = agent.Stats.GetStat(AgentStatIdentifiers.POISE, true);
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

			float currentProtection = protectionStat;
			float currentDefence = poiseStat;

			// --- 1. CRIT LAYER ---
			float effectiveCritChance = Mathf.Clamp01(hitData.CritChance / Mathf.Max(0.001f, 1f + luckStat));
			bool isCrit = !neglect &&
				hitData.CritBonus > 0f &&
				hitData.CritChance > 0f &&
				Random.value < effectiveCritChance;

			float critDamage = isCrit
				? SpaxFormulas.CalculateDamage(hitData.CritBonus, currentProtection, currentDefence)
				: 0f;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- 2. PIERCE LAYER ---
			float pierceDamage = 0f;
			float penetration = 0f;

			if (!neglect && hitData.Pierce > 0f)
			{
				pierceDamage = SpaxFormulas.CalculateDamage(hitData.Pierce, currentProtection, currentDefence);
				// Calculate penetration purely based on physics results
				penetration = Mathf.Clamp01(Mathf.Max(0f, pierceDamage) / hitData.Pierce);
			}
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);

			// --- 3. BLUNT LAYER ---
			float powerRatio = (hitData.Power / (hitData.Power + hitData.Pierce + 0.001f)).Clamp01();
			float impact = (1f - penetration * (1f - powerRatio)).Clamp01();
			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);

			float bluntDamage = 0f;
			float effectiveBluntInput = hitData.Power * impact;

			if (!neglect && effectiveBluntInput > 0f)
			{
				bluntDamage = SpaxFormulas.CalculateDamage(effectiveBluntInput, currentProtection, currentDefence);
			}

			// --- 4. TOTAL PHYSICS DAMAGE ---
			float rawTotalDamage = critDamage + pierceDamage + bluntDamage;
			float damageToTake = Mathf.Max(0f, rawTotalDamage); // Floor at 0 before Grace

			// --- 5. GRACE INTERVENTION ---
			// Applied AFTER physics calculation. It absorbs damage, it does not act as armor.
			if (damageToTake > 0f)
			{
				// Drain Grace
				float drained = statHandler.PointStats.SE.Drain(damageToTake);

				// Reduce final damage by amount successfully drained from Grace
				damageToTake -= drained;
			}

			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE, damageToTake);

			// --- IMPACT & FORCE ---
			float force = hitData.Force * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			float endure = neglect ? 0f : damageToTake + force;
			float enduranceDamage = statHandler.PointStats.W.Drain(
				endure,
				out bool stunned,
				out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			enduranceOverdraw *= endure / (enduranceDamage + enduranceOverdraw).Max(1f);
			float endured = endure > 0f ? (endure - enduranceOverdraw) / endure : 1f;
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

			rigidbodyWrapper.Push(
				hitData.Inertia * (endured.Invert() * 0.5f),
				hitData.HitterMass);

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				float damageDealt = statHandler.PointStats.SW.Drain(damageToTake, out bool dead, out _);
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
					agent.Die();
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

			SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString());
		}
	}
}
