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
		[SerializeField] private float deathDuration = 0.5f;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;

		private EntityStat timescaleStat;
		private EntityStat health;
		private EntityStat endurance;
		private EntityStat defence;
		private EntityStat hardness;

		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(IAgent agent, IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper,
			CombatSettings combatSettings, CallbackService callbackService,
			IStunHandler stunHandler)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
			this.stunHandler = stunHandler;
		}

		protected void OnEnable()
		{
			timescaleStat = agent.GetStat(EntityStatIdentifiers.TIMESCALE, true);
			health = agent.GetStat(AgentStatIdentifiers.HEALTH, true);
			endurance = agent.GetStat(AgentStatIdentifiers.ENDURANCE, true);
			defence = agent.GetStat(AgentStatIdentifiers.DEFENCE, true);
			hardness = agent.GetStat(AgentStatIdentifiers.HARDNESS, true);

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
			// Transfer intertia.
			rigidbodyWrapper.Push(hitData.Inertia, hitData.Mass);

			// Calculate damage and impact.
			if (!hitData.Result_Parried)
			{
				float damage = SpaxFormulas.CalculateDamage(hitData.Offence, defence);
				hitData.Result_Penetration = (hitData.Offence * hitData.Piercing / defence * hardness.Value.InvertClamped()).OutCubic();
				hitData.Result_Impact = (hitData.Strength * hitData.Piercing.InvertClamped() / defence * hardness).OutCubic();
				hitData.Result_Damage = damage * (hitData.Result_Penetration + hitData.Result_Impact);
				hitData.Result_Force = hitData.Strength * hitData.Result_Impact;
			}

			// Apply hit-pause.
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(Mathf.LerpUnclamped(combatSettings.HitPauseRange.x, combatSettings.HitPauseRange.y, hitData.Result_Impact)),
				callbackService);
			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Damage endurance.
			endurance.Damage(hitData.Result_Force, true, out bool stunned);
			if (stunned)
			{
				hitData.Result_Stunned = true;

				// Stun.
				agent.Actor.TryCancel(true);

				// Transfer Impact.
				rigidbodyWrapper.Push(hitData.Direction * hitData.Result_Force, hitData.Force);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}

			// Damage health.
			health.Damage(hitData.Result_Damage, true, out bool dead);
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

			//SpaxDebug.Log($"Hit {agent.Identification.Name}", hitData.ToString(), context: agent.GameObject);
		}

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
