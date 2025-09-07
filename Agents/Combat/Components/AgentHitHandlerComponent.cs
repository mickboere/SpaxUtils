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
		public int Hits { get; set; }

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

			// Calculate damage and impact.
			float damage = 0f, impact = 0f, force = 0f, endured = 0f;
			damage = neglect ? 0f : SpaxFormulas.CalculateDamage(hitData.Offence, defence);
			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE, damage);
			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, neglect ? 0f : damage / hitData.Offence);

			impact = hitData.Power / rigidbodyWrapper.Mass;
			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			force = impact * hitData.Mass * hitData.Power;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// Damage endurance.
			float endure = damage + force;
			float enduranceDamage = statHandler.PointStats.W.Current.Damage(endure, true, out bool stunned, out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);
			enduranceOverdraw *= endure / enduranceDamage.Max(1f); // Compensate for cost-multiplier since overdraw is used in force calculations.
			endured = endure > 0f ? (endure - enduranceOverdraw) / endure : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// Apply stun.
			if (stunned)
			{
				// Cancel all performances.
				agent.Actor.TryCancel(true);

				// Prevent same-frame dodge sliding during stun?
				rigidbodyWrapper.ResetVelocity();

				// Transfer Impact.
				//SpaxDebug.Log($"Impact={hitData.Direction * force * endured.Invert()}", hitData.ToString());
				rigidbodyWrapper.Push(hitData.Direction * force * endured.Invert(), 1f);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}
			else if (neglect)
			{
				// Share Impact.
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// Transfer half intertia.
			rigidbodyWrapper.Push(hitData.Inertia * (endured.Invert() * 0.5f), hitData.HitterMass);

			// Apply damages.
			if (!Invulnerable)
			{
				statHandler.PointStats.SW.Current.Damage(damage, true, out bool dead);
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

			if (hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD) < 0.01f && !neglect)
			{
				Hits++;
			}

			// Build up static for succesful blocking.
			statHandler.PointStats.NE.Current.BaseValue += endured * force * 0.001f;

			// Apply hit-pause.
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.HitPauseReceiver.Lerp(impact) * endured.InvertClamped()),
				callbackService);
			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			//SpaxDebug.Log($"Hit {agent.Identification.Name}", hitData.ToString(), context: agent.GameObject);
		}

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
