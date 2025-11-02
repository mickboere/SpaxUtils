using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class FootstepEffectsComponent : EntityComponentMono
	{
		[SerializeField] private ParticleSystem stepEffect;
		[Header("Stepping")]
		[SerializeField] private int stepEmitAmount = 8;
		[SerializeField] private Vector2Int startSpeed = new Vector2Int(2, 5);
		[Header("Sliding")]
		[SerializeField, Range(0f, 1f)] private float slipThreshold = 0.01f;
		[SerializeField, Range(0f, 100f)] private float minParticleRate = 10f;
		[SerializeField, Range(0f, 100f)] private float maxParticleRate = 25f;

		private AgentLegsComponent legs;
		private RigidbodyWrapper rigidbodyWrapper;
		private GroundedMovementHandler movementHandler;
		private GrounderComponent grounder;

		private Dictionary<Leg, ParticleSystem> effects = new Dictionary<Leg, ParticleSystem>();

		public void InjectDependencies(AgentLegsComponent legs, RigidbodyWrapper rigidbodyWrapper,
			GroundedMovementHandler movementHandler, GrounderComponent grounder)
		{
			this.legs = legs;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.movementHandler = movementHandler;
			this.grounder = grounder;
		}

		protected void OnEnable()
		{
			legs.FootstepEvent += OnFootstepEvent;

			foreach (Leg leg in legs.Legs)
			{
				effects[leg] = Instantiate(stepEffect, Entity.Transform);
			}
		}

		protected void OnDisable()
		{
			legs.FootstepEvent -= OnFootstepEvent;

			foreach (KeyValuePair<Leg, ParticleSystem> effect in effects)
			{
				Destroy(effect.Value.gameObject);
			}
			effects.Clear();
		}

		protected void Update()
		{
			foreach (KeyValuePair<Leg, ParticleSystem> e in effects)
			{
				float slip = grounder.Sliding ?
					(rigidbodyWrapper.Speed / movementHandler.FullSpeed).Clamp01() :
					rigidbodyWrapper.Grip.InvertClamped().OutQuad();
				if (e.Key.Grounded && slip >= slipThreshold)
				{
					// Orientation.
					Orient(e.Key);

					// Settings.
					var main = e.Value.main;
					main.simulationSpeed = Mathf.Max(0.0001f, EntityTimeScale);
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

		private void OnFootstepEvent(Leg leg, bool grounded, Dictionary<SurfaceConfiguration, float> surfaces)
		{
			if (grounded && leg.ValidGround)
			{
				Orient(leg);
				ParticleSystem.MainModule main = effects[leg].main;
				float m = rigidbodyWrapper.Speed / movementHandler.FullSpeed;
				main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed.x * m, startSpeed.y * m);
				effects[leg].Emit(stepEmitAmount);
			}
		}

		private void Orient(Leg leg)
		{
			effects[leg].transform.position = leg.TargetPoint;
			effects[leg].transform.rotation = Quaternion.LookRotation(-Entity.Transform.forward,
				leg.ValidGround ? leg.GroundedHit.normal : Vector3.up);
		}
	}
}
