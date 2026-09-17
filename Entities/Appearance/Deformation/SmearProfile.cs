using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// A held smear: surfaces near its origin trail along it in streaks. Set by <see cref="EntityDeformer"/>.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(SmearProfile), menuName = "ScriptableObjects/Deformation/" + nameof(SmearProfile))]
	public class SmearProfile : ScriptableObject
	{
		[field: Header("Smear")]
		[field: SerializeField, MinMaxRange(0f, 10f), Tooltip("Smear length in meters at zero and full intensity.")]
		public Vector2 Smear { get; private set; } = new Vector2(0f, 0.3f);

		[field: SerializeField, MinMaxRange(0f, 5f)]
		[field: Tooltip("Distance from the origin where the smear has faded out, at zero and full intensity.")]
		public Vector2 Falloff { get; private set; } = new Vector2(1f, 2f);

		[field: SerializeField, MinMaxRange(0f, 1f)]
		[field: Tooltip("How far the feet pin drops in meters at zero and full intensity, so the feet distort.")]
		public Vector2 FloorDrop { get; private set; } = Vector2.zero;

		[field: Header("Streaks")]
		[field: SerializeField, Tooltip("Density of the smear streaks.")]
		public float StreakScale { get; private set; } = 6f;

		[field: SerializeField, Range(0f, 1f), Tooltip("0 = solid smear, 1 = fully broken into streaks.")]
		public float StreakAmount { get; private set; } = 0.75f;

		[field: SerializeField, Tooltip("Speed at which the streaks flow backwards.")]
		public float StreakScroll { get; private set; } = 4f;

		[field: Header("Dither")]
		[field: SerializeField, MinMaxRange(0f, 1f, true)]
		[field: Tooltip("Dither fade on the smeared surfaces only, at zero and full intensity.")]
		public Vector2 Dither { get; private set; } = Vector2.zero;

		[field: SerializeField, MinMaxRange(0f, 1f, true), Tooltip("Whole-body dither fade at zero and full intensity.")]
		public Vector2 BodyDither { get; private set; } = Vector2.zero;

		[field: SerializeField, Tooltip("Speed of the body dither flicker noise.")]
		public float FlickerSpeed { get; private set; } = 12f;

		[field: SerializeField, Range(0f, 1f), Tooltip("Share of the body dither driven by noise: 0 = intensity only.")]
		public float FlickerAmount { get; private set; } = 0.5f;

		/// <summary>
		/// Shader data trailing along <paramref name="direction"/>; <paramref name="time"/> scrolls and flickers.
		/// </summary>
		public DeformSmear Evaluate(Vector3 origin, Vector3 direction, float intensity, float time,
			out float floorDrop, out float bodyFade)
		{
			float t = Mathf.Clamp01(intensity);
			float noise = Mathf.PerlinNoise(time * FlickerSpeed, 0.37f);

			floorDrop = FloorDrop.Lerp(t);
			bodyFade = BodyDither.Lerp(t) * Mathf.Lerp(1f, noise, FlickerAmount);

			return new DeformSmear
			{
				Vector = direction.normalized * Smear.Lerp(t),
				Origin = origin,
				Falloff = Falloff.Lerp(t),
				Phase = time * StreakScroll,
				Scroll = StreakScroll,
				StreakScale = StreakScale,
				StreakAmount = StreakAmount,
				Dither = Dither.Lerp(t),
			};
		}
	}
}
