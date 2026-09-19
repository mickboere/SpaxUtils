using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The non-clip half of an SFX. A <see cref="TieredSFX"/> holds one as its shared default,
	/// which any <see cref="SFXTier"/> may override.
	/// </summary>
	[Serializable]
	public class SFXSettings
	{
		public Vector2 VolumeRange => volumeRange;
		public Vector2 PitchRange => pitchRange;
		public Vector2 DistanceRange => distanceRange;

		[SerializeField, MinMaxRange(0.01f, 1f, true)] private Vector2 volumeRange = Vector2.one;
		[SerializeField, MinMaxRange(0.01f, 3f, true, false)] private Vector2 pitchRange = Vector2.one;
		[SerializeField, MinMaxRange(0.1f, 10f, true, false), Tooltip("How far it carries, across the INTENSITY range - a scratch is heard from far less far away than a massacre.")]
		private Vector2 distanceRange = Vector2.one;

		// Lerped by intensity, not the roll: the ladder answers magnitude by swapping clips,
		// which distance cannot do, so it has to read the magnitude itself.
		public float MinDistance(float intensity) => distanceRange.Lerp(intensity) * SFXData.DISTANCE_MIN;
		public float MaxDistance(float intensity) => distanceRange.Lerp(intensity) * SFXData.DISTANCE_MAX;
	}
}
