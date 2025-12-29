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

		[SerializeField] private float deathDuration = 0.5f;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;
		private AgentStatHandler statHandler;

		private EntityStat timescaleStat;
		private EntityStat poiseStat;
		private EntityStat protectionStat;
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
			poiseStat = agent.Stats.GetStat(AgentStatIdentifiers.POISE, true);          // curved defence
			protectionStat = agent.Stats.GetStat(AgentStatIdentifiers.PROTECTION, true); // linear protection
			luckStat = agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);

			hittable.Subscribe(this, OnHitEvent, 100);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
		}

		/// <summary>
		/// Invoked when this agent has been hit by an attack.
		/// </summary>
		private void OnHitEvent(HitData hitData)
		{
			bool blocked = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);
			bool parried = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED);
			bool deflected = hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);
			bool neglect = blocked || parried || deflected;

			// --- CRIT DAMAGE ---
			float effectiveCritChance = Mathf.Clamp01(hitData.CritChance / Mathf.Max(0.001f, 1f + luckStat));
			bool isCrit = !neglect &&
				hitData.CritBonus > 0f &&
				hitData.CritChance > 0f &&
				Random.value < effectiveCritChance;
			float critDamage =
				!neglect && isCrit ?
					SpaxFormulas.CalculateDamage(hitData.CritBonus, protectionStat, poiseStat) :
					0f;
			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- PIERCE DAMAGE ---
			float pierceDamage = 0f;
			float penetration = 0f;
			if (!neglect && hitData.Pierce > 0f)
			{
				pierceDamage = SpaxFormulas.CalculateDamage(hitData.Pierce, protectionStat, poiseStat);
				penetration = Mathf.Clamp01(pierceDamage / hitData.Pierce);
			}
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);

			// --- BLUNT DAMAGE ---
			float powerRatio = (hitData.Power / (hitData.Power + hitData.Pierce + 0.001f)).Clamp01();
			float impact = (1f - penetration * (1f - powerRatio)).Clamp01();
			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			float bluntDamage =
				!neglect && hitData.Power > 0f && impact > 0f ?
					SpaxFormulas.CalculateDamage(hitData.Power * impact, protectionStat, poiseStat) :
					0f;

			// --- TOTAL DAMAGE ---
			float totalDamage = critDamage + pierceDamage + bluntDamage;
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE, totalDamage);

			// --- IMPACT & FORCE ---

			// Physical force magnitude from the hitter.
			float force = hitData.Force * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			// Endurance sees both sharp damage and force.
			float endure = neglect ? 0f : totalDamage + force;
			float enduranceDamage = statHandler.PointStats.W.Current.Damage(
				endure,
				true,
				out bool stunned,
				out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			// Account for cost multipliers: how much of the intended input was actually endured.
			enduranceOverdraw *= endure / enduranceDamage.Max(1f);
			float endured = endure > 0f ? (endure - enduranceOverdraw) / endure : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// --- STUN / IMPACT APPLICATION ---
			if (stunned)
			{
				// Cancel all performances.
				agent.Actor.TryCancel(true);

				// Prevent same-frame dodge sliding during stun.
				rigidbodyWrapper.ResetVelocity();

				// Transfer force, reduced by what was endured.
				rigidbodyWrapper.Push(hitData.Direction * force * endured.Invert(), 1f);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}
			else if (neglect)
			{
				// Share impact for block/parry/deflect.
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// Transfer half inertia, scaled by what was not endured.
			rigidbodyWrapper.Push(
				hitData.Inertia * (endured.Invert() * 0.5f),
				hitData.HitterMass);

			// --- HP DAMAGE APPLICATION ---
			if (!Invulnerable)
			{
				float damageDealt = statHandler.PointStats.SW.Current.Damage(totalDamage, true, out bool dead);
				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT, damageDealt);

				if (dead)
				{
					if (stunHandler.Stunned)
					{
						stunHandler.ExitedStunEvent += Die;
					}
					else
					{
						Die();
					}
				}
			}

			// Build up Static for successful blocking / enduring.
			statHandler.PointStats.NE.Current.BaseValue += endured * force * 0.001f;

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

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
