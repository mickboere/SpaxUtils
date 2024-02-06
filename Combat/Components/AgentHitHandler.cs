﻿using System.Collections.Generic;
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

		private EntityStat health;
		private EntityStat endurance;
		private EntityStat defence;

		private HitData lastHit;
		private Timer stunTimer;
		private FloatOperationModifier stunControlMod;

		public void InjectDependencies(IAgent agent, IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
		}

		protected void OnEnable()
		{
			hittable.Subscribe(this, OnHitEvent, -1);
			stunControlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, stunControlMod);

			health = agent.GetStat(AgentStatIdentifiers.HEALTH);
			endurance = agent.GetStat(AgentStatIdentifiers.ENDURANCE);
			defence = agent.GetStat(AgentStatIdentifiers.DEFENCE);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
			rigidbodyWrapper.Control.RemoveModifier(this);
			stunControlMod.Dispose();
		}

		protected void Update()
		{
			if (stunTimer)
			{
				PoserStruct instructions = hitBlendTree.GetInstructions(-lastHit.Direction.Localize(rigidbodyWrapper.transform), 0f);
				animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunTimer.Progress.ReverseInOutCubic());
				stunControlMod.SetValue(stunTimer.Progress.InOutCubic());
			}
			else
			{
				animatorPoser.RevokeInstructions(this);
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
			float damage = SpaxFormulas.CalculateDamage(hitData.Offence, defence);
			float penetration = hitData.Offence * hitData.Piercing / defence;
			float impact = hitData.Strength * penetration.InvertClamped();

			// TODO: Apply hit-pause depending on penetration on both ends, return it through HitData?.

			// Apply damage.
			health.Damage(damage, out bool dead);
			if (dead)
			{
				// TODO: Die!
			}

			// Apply impact.
			endurance.Damage(impact, out bool stunned);
			if (stunned)
			{
				// Stunned.
				agent.Actor.TryCancel(true);

				// Transfer Impact.
				rigidbodyWrapper.AddImpact(hitData.Direction * impact, hitData.Mass);

				float stunTime = hitData.Strength / rigidbodyWrapper.Mass;
				stunTimer = new Timer(Mathf.Min(stunTime, maxStunTime));
			}
		}
	}
}
