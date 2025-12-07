using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling hits coming through in the <see cref="IHittable"/> component.
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
		private EntityStat defence;
		private EntityStat guard;
		private EntityStat luck;

		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(IAgent agent, IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper,
			CombatSettings combatSettings, CallbackService callbackService,
			IStunHandler stunHandler, AgentStatHandler statHandler)
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
			defence = agent.Stats.GetStat(AgentStatIdentifiers.DEFENCE, true);
			guard = agent.Stats.GetStat(AgentStatIdentifiers.GUARD, true);
			luck = agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);

			hittable.Subscribe(this, OnHitEvent, 100);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
		}

		/// <summary>
		/// Invoked when this agent has been hit by an attack.
		/// </summary>
		/// <param name="hitData">The incoming <see cref="HitData"/> to process.</param>
		private void OnHitEvent(HitData hitData)
		{
			bool blocked = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);
			bool parried = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED);
			bool deflected = hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);
			bool neglect = blocked || parried || deflected;

			// --- CRIT ROLL (uses hitData.CritChance, reduced by Luck) ---
			float effectiveCritChance = Mathf.Clamp01(hitData.CritChance * (1f - luck));
			bool isCrit = !neglect && UnityEngine.Random.value < effectiveCritChance;
			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);

			// --- SHARP DAMAGE (Offence vs Defence) ---
			float sharpDamage = 0f;
			if (!neglect && hitData.Offence > 0f)
			{
				// DEFENCE is effectively hardness here.
				sharpDamage = SpaxFormulas.CalculateDamage(hitData.Offence, defence);
			}

			// Penetration: how much of Offence actually turned into sharp HP damage (0..1).
			float penetration = 0f;
			if (hitData.Offence > 0f)
			{
				penetration = Mathf.Clamp01(sharpDamage / hitData.Offence);
			}
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);

			// --- BLUNT DAMAGE (Power weighted by inverse penetration) ---
			// Power acts as blunt "offence"; (1 - penetration) is the blunt factor.
			float bluntOffence = hitData.Power * (1f - penetration);

			float bluntDamage = 0f;
			if (!neglect && bluntOffence > 0f)
			{
				bluntDamage = SpaxFormulas.CalculateDamage(bluntOffence, defence);
			}

			// --- TOTAL HP DAMAGE ---
			float totalDamage = sharpDamage + bluntDamage;
			if (isCrit)
			{
				totalDamage *= Mathf.Max(1f, hitData.CritMult);
			}
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE, totalDamage);

			// --- IMPACT & FORCE ---
			// Impact is the bluntness factor: 0 = fully sharp, 1 = fully blunt.
			float impact = 1f - penetration;
			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);

			// Physical force magnitude still comes from mass * power.
			float force = hitData.Force;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			// Crit should influence Endurance drain as well.
			float endureInput = totalDamage + force;
			float enduranceDamage = statHandler.PointStats.W.Current.Damage(
				endureInput,
				true,
				out bool stunned,
				out float enduranceOverdraw);

			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			enduranceOverdraw *= endureInput / enduranceDamage.Max(1f); // compensate for cost multiplier
			float endured = endureInput > 0f ? (endureInput - enduranceOverdraw) / endureInput : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// --- STUN / IMPACT APPLICATION ---
			if (stunned)
			{
				// Cancel all performances.
				agent.Actor.TryCancel(true);

				// Prevent same-frame dodge sliding during stun.
				rigidbodyWrapper.ResetVelocity();

				// Transfer Impact (reduced by what was endured).
				rigidbodyWrapper.Push(hitData.Direction * force * endured.Invert(), 1f);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}
			else if (neglect)
			{
				// Share Impact for block/parry/deflect.
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// Transfer half inertia.
			rigidbodyWrapper.Push(hitData.Inertia * (endured.Invert() * 0.5f), hitData.HitterMass);

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

			// Build up static for succesful blocking.
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
		}

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
