using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling hits coming through in the <see cref="IHittable"/> component.
	/// </summary>
	public class AgentHitHandler : EntityComponentBase
	{
		[SerializeField] private PoseSequenceBlendTree hitBlendTree;
		[SerializeField] private float maxStunTime = 3f;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private CombatSettings combatSettings;
		private CallbackService callbackService;

		private EntityStat timescaleStat;
		private EntityStat health;
		private EntityStat endurance;
		private EntityStat defence;

		private HitData lastHit;
		private TimerClass stunTimer;
		private FloatOperationModifier stunControlMod;
		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(IAgent agent, IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser,
			CombatSettings combatSettings, CallbackService callbackService)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
		}

		protected void OnEnable()
		{
			hittable.Subscribe(this, OnHitEvent, -1);
			stunControlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, stunControlMod);

			timescaleStat = agent.GetStat(EntityStatIdentifiers.TIMESCALE);
			health = agent.GetStat(AgentStatIdentifiers.HEALTH);
			endurance = agent.GetStat(AgentStatIdentifiers.ENDURANCE);
			defence = agent.GetStat(AgentStatIdentifiers.DEFENCE);

			stunTimer = new TimerClass(null, () => EntityTimeScale, true);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
			rigidbodyWrapper.Control.RemoveModifier(this);
		}

		protected void Update()
		{
			if (!stunTimer.Expired)
			{
				PoserStruct instructions = hitBlendTree.GetInstructions(-lastHit.Direction.Localize(rigidbodyWrapper.transform), 0f);
				animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunTimer.Progress.ReverseInOutCubic());
				stunControlMod.SetValue(stunTimer.Progress.InOutSine());
				agent.Actor.Blocked = true;
			}
			else
			{
				animatorPoser.RevokeInstructions(this);
				stunControlMod.SetValue(1f);
				agent.Actor.Blocked = false;
			}
		}

		private void OnHitEvent(HitData hitData)
		{
			lastHit = hitData;

			// Transfer intertia.
			if (hitData.Hitter.TryGetStat(EntityStatIdentifiers.MASS, out EntityStat hitterMass))
			{
				rigidbodyWrapper.AddImpact(hitData.Inertia, hitterMass);
			}

			// Calculate damage and impact.
			hitData.Penetration = hitData.Parried ? 0f : hitData.Offence * hitData.Piercing / defence;
			float absorbtion = hitData.Penetration.InvertClamped().OutCubic();//.Range(combatSettings.MinAbsorbtion, combatSettings.MaxAbsorbtion);
			float damage = hitData.Parried ? 0f : SpaxFormulas.CalculateDamage(hitData.Offence, defence);
			float impact = hitData.Parried ? 0f : hitData.Strength * absorbtion;

			// Apply hit-pause.
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.MaxHitPause * absorbtion),
				callbackService);
			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Damage endurance.
			endurance.Damage(damage + impact, out bool stunned);
			if (stunned)
			{
				// Stunned.
				agent.Actor.TryCancel(true);

				// Transfer Impact.
				rigidbodyWrapper.AddImpact(hitData.Direction * impact, hitData.Mass);

				// Apply stun.
				// TODO: Should be based on actual stun state that has a minimum duration for low impact forces and a control-detector for big impacts that send the agent flying or sliding away.
				float stunTime = impact * hitData.Mass / rigidbodyWrapper.Mass;
				stunTimer.Reset(Mathf.Min(stunTime, maxStunTime));
			}

			// Damage health.
			health.Damage(damage, out bool dead);
			if (dead)
			{
				// TODO: Die! Should be applied after impact and stun have been processed.
			}
		}
	}
}
