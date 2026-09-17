using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Body measurements the deformation needs, from an <see cref="ITargetable"/> or a transform fallback.
	/// </summary>
	public static class DeformBody
	{
		public const float MIN_PIN_HEIGHT = 0.1f;
		private const float FALLBACK_HEIGHT = 1.8f;

		/// <summary>
		/// Feet pin and travel size of a body: root height, world-space height and bounds diagonal.
		/// </summary>
		public static void GetBody(ITargetable targetable, Transform fallback,
			out float floor, out float height, out float size)
		{
			if (targetable != null)
			{
				Vector3 bounds = targetable.Size;
				floor = targetable.Position.y;
				height = bounds.y;
				size = bounds.magnitude;
			}
			else
			{
				floor = fallback.position.y;
				height = FALLBACK_HEIGHT;
				size = FALLBACK_HEIGHT;
			}

			height = Mathf.Max(height, MIN_PIN_HEIGHT);
		}

		/// <summary>
		/// World-space center of a body.
		/// </summary>
		public static Vector3 GetCenter(ITargetable targetable, Transform fallback)
		{
			return targetable != null ? targetable.Center : fallback.position + Vector3.up * FALLBACK_HEIGHT * 0.5f;
		}

		/// <summary>
		/// Point on the body's surface opposite <paramref name="direction"/>, e.g. the leading side for a push back.
		/// </summary>
		public static Vector3 GetLaunchPoint(ITargetable targetable, Transform fallback, Vector3 direction)
		{
			Vector3 dir = direction.normalized;
			Vector3 extents = (targetable != null ? targetable.Size : Vector3.one * FALLBACK_HEIGHT) * 0.5f;
			float depth = Mathf.Abs(dir.x) * extents.x + Mathf.Abs(dir.y) * extents.y + Mathf.Abs(dir.z) * extents.z;
			return GetCenter(targetable, fallback) - dir * depth;
		}
	}
}
