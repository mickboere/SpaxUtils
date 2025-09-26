using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;
		public SFXData SlideSFX => slideSFX;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField, Range(0f, 1f)] private float hardness = 1f;
		[SerializeField] private ImpactSFXData[] impactSFX;
		[SerializeField] private SFXData slideSFX;

		public SFXData GetImpactSFX(float impactForce)
		{
			if (impactSFX.Length == 0)
			{
				return null;
			}
			else if (impactSFX.Length == 1)
			{
				return impactSFX[0].SFX;
			}
			else
			{
				ImpactSFXData match = impactSFX[0];
				for (int i = 1; i < impactSFX.Length; i++)
				{
					if (impactForce > impactSFX[i].Force && impactSFX[i].Force > match.Force)
					{
						match = impactSFX[i];
					}
				}
				return match.SFX;
			}
		}
	}
}
