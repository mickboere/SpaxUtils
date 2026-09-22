using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[Serializable]
	public class SurfaceConfiguration
	{
		public string Surface => surface;
		public float Hardness => hardness;

		/// <summary>What this sounds like when something lands ON it.</summary>
		public TieredSFX StruckSFX => struckTiers;

		/// <summary>What this sounds like as the thing doing the landing.</summary>
		public TieredSFX StrikeSFX => strikeTiers;

		public SFXData SlideSFX => slideSFX;

		[SerializeField, ConstDropdown(typeof(ISurfaceTypeConstants))] private string surface;
		[SerializeField, Range(0f, 1f)] private float hardness = 1f;
		[SerializeField, FormerlySerializedAs("impactTiers"), Tooltip("Struck: what this sounds like when something lands on it.")]
		private TieredSFX struckTiers = new TieredSFX();
		[SerializeField, Tooltip("Striking: what this sounds like as the thing landing. Empty for anything never swung or stepped with.")]
		private TieredSFX strikeTiers = new TieredSFX();
		[SerializeField] private SFXData slideSFX;
	}
}
