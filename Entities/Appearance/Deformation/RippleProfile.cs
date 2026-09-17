using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// A one-shot ripple through a body, e.g. a hit or a dash launch. Played by <see cref="EntityDeformer"/>.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(RippleProfile), menuName = "ScriptableObjects/Deformation/" + nameof(RippleProfile))]
	public class RippleProfile : ScriptableObject
	{
		[field: Header("Shape")]
		[field: SerializeField, Tooltip("Speed in m/s at which the ripple front spreads from the impact axis.")]
		public float Speed { get; private set; } = 6f;

		[field: SerializeField, Tooltip("Distance between ripple rings in meters.")]
		public float Length { get; private set; } = 0.25f;

		[field: SerializeField, Tooltip("Distance behind the front over which rings fade out, in meters.")]
		public float Decay { get; private set; } = 0.6f;

		[field: SerializeField, Tooltip("How much ring spacing widens behind the front: 0 = even, 1 = doubled at Decay.")]
		public float Stretch { get; private set; } = 1f;

		[field: SerializeField, Tooltip("Sideways push away from the impact axis, as a fraction of the forward push.")]
		public float Outward { get; private set; } = 0.35f;

		[field: SerializeField, Tooltip("Pull back against the push direction: 0 = none, 1 = symmetric ripple.")]
		public float Rebound { get; private set; } = 0.15f;

		[field: Header("Strength")]
		[field: SerializeField, Tooltip("Offset over the ripple's trip through the body (0 → 1 → 0).")]
		public AnimationCurve Curve { get; private set; } = new AnimationCurve(
			new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

		[field: SerializeField, MinMaxRange(0f, 2f), Tooltip("Crown displacement in meters at zero and full intensity.")]
		public Vector2 Amplitude { get; private set; } = new Vector2(0.02f, 0.06f);

		[field: SerializeField, MinMaxRange(0f, 5f)]
		[field: Tooltip("Distance from the impact point where the ripple has faded out, at zero and full intensity.")]
		public Vector2 Falloff { get; private set; } = new Vector2(0.4f, 1.2f);

		/// <summary>
		/// Shader data <paramref name="time"/> seconds in; false once the rings have left a body of <paramref name="bodySize"/>.
		/// </summary>
		public bool Evaluate(Vector3 point, Vector3 direction, float intensity, float time, float bodySize,
			out DeformRipple ripple)
		{
			float t = Mathf.Clamp01(intensity);
			float travel = Mathf.Max(bodySize + Decay, 0.01f);
			float radius = Mathf.Max(Speed, 0f) * time;
			float progress = Speed > 0f ? radius / travel : 1f;

			ripple = new DeformRipple
			{
				Point = point,
				Direction = direction.normalized,
				Amplitude = Amplitude.Lerp(t) * Curve.Evaluate(Mathf.Clamp01(progress)),
				Radius = radius,
				Length = Length,
				Decay = Decay,
				Falloff = Falloff.Lerp(t),
				Stretch = Stretch,
				Outward = Outward,
				Rebound = Rebound,
			};
			return progress < 1f;
		}
	}
}
