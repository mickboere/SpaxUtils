using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling hits coming through in the <see cref="IHittable"/> component.
	/// </summary>
	public class AgentHitHandlerComponent : EntityComponentBase
	{
		[SerializeField] private float deathDuration = 0.5f;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;

		private EntityStat timescaleStat;
		private EntityStat health;
		private EntityStat endurance;
		private EntityStat defence;

		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(IAgent agent, IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser,
			CombatSettings combatSettings, CallbackService callbackService,
			IStunHandler stunHandler)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
			this.stunHandler = stunHandler;
		}

		protected void OnEnable()
		{
			hittable.Subscribe(this, OnHitEvent, -1);

			timescaleStat = agent.GetStat(EntityStatIdentifiers.TIMESCALE);
			health = agent.GetStat(AgentStatIdentifiers.HEALTH);
			endurance = agent.GetStat(AgentStatIdentifiers.ENDURANCE);
			defence = agent.GetStat(AgentStatIdentifiers.DEFENCE);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
			rigidbodyWrapper.Control.RemoveModifier(this);
		}

		/// <summary>
		/// Invoked when this agent has been hit by an attack.
		/// </summary>
		/// <param name="hitData">The incoming <see cref="HitData"/> to process.</param>
		private void OnHitEvent(HitData hitData)
		{
			// Transfer intertia.
			rigidbodyWrapper.Push(hitData.Inertia, hitData.HitterMass);

			// Calculate damage and impact.
			hitData.Penetration = hitData.Parried ? 0f : hitData.Offence * hitData.Piercing / defence;
			float impact = hitData.Penetration.InvertClamped();
			float damage = hitData.Parried ? 0f : SpaxFormulas.CalculateDamage(hitData.Offence, defence);
			float force = hitData.Parried ? 0f : hitData.Strength * impact;

			// Apply hit-pause.
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.MaxHitPause * impact),
				callbackService);
			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Damage endurance.
			endurance.Damage(force, true, out bool stunned);
			if (stunned)
			{
				// Stunned.
				agent.Actor.TryCancel(true);

				// Transfer Impact.
				rigidbodyWrapper.Push(hitData.Direction * force, hitData.Mass);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}

			// Damage health.
			health.Damage(damage, true, out bool dead);
			if (dead)
			{
				// TODO: Die! Should be applied after impact and stun have been processed.
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

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
