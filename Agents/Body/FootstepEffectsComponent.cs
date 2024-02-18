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
			foreach (KeyValuePair<ILeg, ParticleSystem> e in effects)
			{
				float slip = rigidbodyWrapper.Grip.Invert().OutQuad();
				if (e.Key.Grounded && slip >= slipThreshold)
				{
					// Orientation.
					Orient(e.Key);

					// Settings.
					var main = e.Value.main;
					main.simulationSpeed = EntityTimeScale;
					var emission = e.Value.emission;
					emission.rateOverTime = Mathf.Lerp(minParticleRate, maxParticleRate, slip);

					if (e.Value.isStopped)
					{
						// Play.
						e.Value.Play();
					}
				}
				else if (e.Value.isPlaying)
				{
					e.Value.Stop();
				}
			}
		}

		private void OnFootstepEvent(ILeg leg, bool grounded)
		{
			if (grounded && leg.ValidGround)
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
