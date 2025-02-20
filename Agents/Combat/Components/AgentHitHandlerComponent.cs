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
		private EntityStat hardness;

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
			hardness = agent.Stats.GetStat(AgentStatIdentifiers.HARDNESS, true);

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
			rigidbodyWrapper.Push(hitData.Inertia, hitData.HitterMass);

			// Calculate damage and impact.
			if (!hitData.Result_Parried)
			{
				float damage = SpaxFormulas.CalculateDamage(hitData.Offence, defence);
				hitData.Result_PenetrationPercentile = hitData.Offence * hitData.Piercing / defence;
				float impact = (hitData.Hardness - hardness).Clamp01();
				hitData.Result_ImpactPercentile = (hitData.Power * impact + hitData.Power * hitData.Piercing.InvertClamped()) * 0.5f / defence;
				hitData.Result_Damage = damage * (hitData.Result_PenetrationPercentile + hitData.Result_ImpactPercentile);
				hitData.Result_Force = hitData.Mass * hitData.Power * hitData.Result_ImpactPercentile;
			}

			statHandler.PointStatOctad.NE.Current.BaseValue += hitData.Result_Force * hitData.Result_Blocked;

			// Apply hit-pause.
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(Mathf.LerpUnclamped(combatSettings.HitPauseRange.x, combatSettings.HitPauseRange.y, hitData.Result_ImpactPercentile)),
				callbackService);
			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Damage endurance.
			statHandler.PointStatOctad.W.Current.Damage(hitData.Power, true, out bool stunned, out float overdraw);
			if (stunned)
			{
				hitData.Result_Stunned = true;

				// Stun.
				agent.Actor.TryCancel(true);

				// Transfer Impact.
				rigidbodyWrapper.Push(hitData.Direction * hitData.Result_Force, 1f);

				// Apply stun.
				stunHandler.EnterStun(hitData);
			}

			if (!Invulnerable)
			{
				// Damage health.
				statHandler.PointStatOctad.SW.Current.Damage(hitData.Result_Damage, true, out bool dead);
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

			if (stunned || hitData.Result_Blocked.Approx(0f) && !hitData.Result_Parried)
			{
				Hits++;
			}

			SpaxDebug.Log($"Hit {agent.Identification.Name}", hitData.ToString(), context: agent.GameObject);
		}

		private void Die()
		{
			stunHandler.ExitedStunEvent -= Die;
			agent.Die(new TimedStateTransition(deathDuration));
		}
	}
}
