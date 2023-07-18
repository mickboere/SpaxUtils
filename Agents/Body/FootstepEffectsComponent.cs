using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class FootstepEffectsComponent : EntityComponentBase
	{
		[SerializeField] private ParticleSystem stepEffect;
		[SerializeField, Range(0f, 100f)] private float minParticleRate = 10f;
		[SerializeField, Range(0f, 100f)] private float maxParticleRate = 25f;
		[SerializeField, Range(0f, 1f)] private float slipThreshold = 0.01f;
		[SerializeField] private int stepEmitAmount = 3;

		private ILegsComponent legs;
		private RigidbodyWrapper rigidbodyWrapper;

		private Dictionary<ILeg, ParticleSystem> effects = new Dictionary<ILeg, ParticleSystem>();

		public void InjectDependencies(ILegsComponent legs, RigidbodyWrapper rigidbodyWrapper)
		{
			this.legs = legs;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		protected void OnEnable()
		{
			legs.FootstepEvent += OnFootstepEvent;

			foreach (ILeg leg in legs.Legs)
			{
				effects[leg] = Instantiate(stepEffect, Entity.Transform);
			}
		}

		protected void OnDisable()
		{
			legs.FootstepEvent -= OnFootstepEvent;

			foreach (KeyValuePair<ILeg, ParticleSystem> effect in effects)
			{
				Destroy(effect.Value.gameObject);
			}
			effects.Clear();
		}

		protected void Update()
		{
			foreach (ILeg leg in effects.Keys)
			{
				float slip = rigidbodyWrapper.Grip.InQuad().Invert();
				if (leg.Grounded && slip >= slipThreshold)
				{
					// Orientation.
					Orient(leg);

					// Settings.
					var main = effects[leg].main;
					main.simulationSpeed = EntityTimeScale;
					var emission = effects[leg].emission;
					emission.rateOverTime = Mathf.Lerp(minParticleRate, maxParticleRate, slip);

					if (effects[leg].isStopped)
					{
						// Play.
						effects[leg].Play();
					}
				}
				else if (effects[leg].isPlaying)
				{
					effects[leg].Stop();
				}
			}
		}

		private void OnFootstepEvent(ILeg leg, bool grounded)
		{
			if (grounded)
			{
				Orient(leg);
				effects[leg].Emit(stepEmitAmount);
			}
		}

		private void Orient(ILeg leg)
		{
			effects[leg].transform.position = leg.TargetPoint;
			effects[leg].transform.rotation = Quaternion.LookRotation(-Entity.Transform.forward, leg.GroundedHit.normal);
		}
	}
}