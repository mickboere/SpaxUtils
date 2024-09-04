using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentFeetAudio : EntityComponentBase
	{
		[SerializeField] private AudioSource feetAudioSource;

		private SurfaceLibrary surfaceLibrary;
		private ILegsComponent legs;

		public void InjectDependencies(SurfaceLibrary surfaceLibrary, ILegsComponent legs)
		{
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

			if (SurfaceComponent.TryGetSurfaceValues(leg.GroundedHit, out Dictionary<string, float> surfaces) && surfaces.Count > 0)
			{
				foreach (KeyValuePair<string, float> surface in surfaces)
				{
					SurfaceConfiguration config = surfaceLibrary.Get(surface.Key);
					if (config != null)
					{
						config.GetImpactSFX(0f).PlayOneShot(feetAudioSource, surface.Value);
					}
				}
			}
			else
			{
				SurfaceConfiguration config = surfaceLibrary.Get(DefaultSurfaceTypes.DEFAULT);
				config.GetImpactSFX(0f).PlayOneShot(feetAudioSource);
			}
		}
	}
}
