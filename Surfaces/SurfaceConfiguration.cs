using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;
		public float Hardness => hardness;

		/// <summary>What this sounds like when something lands ON it.</summary>
		public TieredSFX ImpactSFX => impactTiers;

		/// <summary>What this sounds like as the thing doing the landing.</summary>
		public TieredSFX StrikeSFX => strikeTiers;

		public SFXData SlideSFX => slideSFX;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField, Range(0f, 1f)] private float hardness = 1f;
		[SerializeField, Tooltip("Struck: what this sounds like when something lands on it.")]
		private TieredSFX impactTiers = new TieredSFX();
		[SerializeField, Tooltip("Striking: what this sounds like as the thing landing. Empty for anything never swung or stepped with.")]
		private TieredSFX strikeTiers = new TieredSFX();
		[SerializeField] private SFXData slideSFX;
	}
}
