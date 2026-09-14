using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;
		public float Hardness => hardness;
		public SFXData SlideSFX => slideSFX;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField, Range(0f, 1f)] private float hardness = 1f;
		[SerializeField] private ImpactSFXData[] impactSFX;
		[SerializeField] private SFXData slideSFX;

		public SFXData GetImpactSFX(float impactForce)
		{
			return ImpactSFXData.Select(impactSFX, impactForce);
		}
	}
}
