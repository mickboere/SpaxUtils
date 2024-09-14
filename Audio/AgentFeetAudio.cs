using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentFeetAudio : EntityComponentBase
	{
		[SerializeField] private AudioSource feetAudioSource;
		[SerializeField, Tooltip("X is agent velocity, Y is step volume. X=1 when agent is moving at 'max' speed * 1.5f.")] private AnimationCurve intensityCurve;

		private IAgent agent;
		private SurfaceLibrary surfaceLibrary;
		private ILegsComponent legs;

		public void InjectDependencies(IAgent agent, SurfaceLibrary surfaceLibrary, ILegsComponent legs)
		{
			this.agent = agent;
			this.legs = legs;
			this.surfaceLibrary = surfaceLibrary;
		}

		protected void OnEnable()
		{
			legs.FootstepEvent += OnFootstepEvent;
		}

		protected void OnDisable()
		{
			legs.FootstepEvent -= OnFootstepEvent;
		}

		private void OnFootstepEvent(ILeg leg, bool grounded)
		{
			if (!grounded)
			{
				return;
			}

			float intensity = intensityCurve.Evaluate(agent.Body.RigidbodyWrapper.Speed / agent.Body.BaseSpeed * 1.5f);
			if (SurfaceComponent.TryGetSurfaceValues(leg.GroundedHit, out Dictionary<string, float> surfaces) && surfaces.Count > 0)
			{
				foreach (KeyValuePair<string, float> surface in surfaces)
				{
					SurfaceConfiguration config = surfaceLibrary.Get(surface.Key);
					if (config != null)
					{
						config.GetImpactSFX(0f).PlayOneShot(feetAudioSource, surface.Value * intensity);
					}
				}
			}
			else
			{
				SurfaceConfiguration config = surfaceLibrary.Get(DefaultSurfaceTypes.DEFAULT);
				config.GetImpactSFX(0f).PlayOneShot(feetAudioSource, intensity);
			}
		}
	}
}
