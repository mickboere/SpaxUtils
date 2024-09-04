using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField] private ImpactSFXData[] impactSFX;

		public SFXData GetImpactSFX(float impact)
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
					if (impact > impactSFX[i].Force && impactSFX[i].Force > match.Force)
					{
						match = impactSFX[i];
					}
				}
				return match.SFX;
			}
		}
	}
}
