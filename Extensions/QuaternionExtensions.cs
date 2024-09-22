using UnityEngine;

namespace SpaxUtils
{
	public static class QuaternionExtensions
	{
		public static Quaternion Lerp(this Quaternion a, Quaternion b, float t)
		{
			return Quaternion.Lerp(a, b, t);
		}

		public static Quaternion Slerp(this Quaternion a, Quaternion b, float t)
		{
			return Quaternion.Slerp(a, b, t);
		}

		public static Quaternion Inverse(this Quaternion a)
		{
			return Quaternion.Inverse(a);
		}

		/// <summary>
		/// Clamps <paramref name="quaternion"/>'s <paramref name="direction"/> within <paramref name="maxAngle"/> from <paramref name="axis"/>.
		/// </summary>
		public static Quaternion Clamp(this Quaternion quaternion, Vector3 direction, Vector3 axis, float maxAngle)
		{
			float angle = Vector3.Angle(quaternion * direction, axis);
			if (angle > maxAngle)
			{
				quaternion = Quaternion.Lerp(quaternion, Quaternion.LookRotation(axis), 1f - (maxAngle / angle));
			}
			return quaternion;
		}

		/// <summary>
		/// Clamps <paramref name="quaternion"/> within <paramref name="maxAngle"/> from <paramref name="axis"/>.
		/// </summary>
		public static Quaternion ClampForward(this Quaternion quaternion, Vector3 axis, float maxAngle)
		{
			float angle = Vector3.Angle(quaternion * Vector3.forward, axis);
			if (angle > maxAngle)
			{
				quaternion = Quaternion.Lerp(quaternion, Quaternion.LookRotation(axis), 1f - (maxAngle / angle));
			}
			return quaternion;
		}

		/// <summary>
		/// Smoothly clamps <paramref name="quaternion"/> within <paramref name="maxAngle"/> from <paramref name="axis"/>, never exceeding <paramref name="absoluteMaxAngle"/>.
		/// </summary>
		public static Quaternion SmoothClampForward(this Quaternion quaternion, Vector3 axis, float maxAngle, float absoluteMaxAngle, float smoothPower)
		{
			float angle = Vector3.Angle(quaternion * Vector3.forward, axis);
			if (angle > absoluteMaxAngle)
			{
				quaternion = Quaternion.Lerp(quaternion, Quaternion.LookRotation(axis), 1f - (absoluteMaxAngle / angle));
			}
			else if (angle > maxAngle)
			{
				float smooth = Mathf.InverseLerp(maxAngle, absoluteMaxAngle, angle);
				quaternion = Quaternion.Lerp(quaternion, Quaternion.LookRotation(axis), smooth * smoothPower);
			}
			return quaternion;
		}

		public static Quaternion ResetZRotation(this Quaternion quaternion)
		{
			quaternion = Quaternion.LookRotation(quaternion * Vector3.forward, Vector3.up);
			return quaternion;
		}

		public static Quaternion Average(params Quaternion[] quaternions)
		{
			Vector3 forward = Vector3.zero;
			Vector3 up = Vector3.zero;
			foreach (Quaternion q in quaternions)
			{
				forward += q * Vector3.forward;
				up += q * Vector3.up;
			}
			return Quaternion.LookRotation(forward / quaternions.Length, up / quaternions.Length);
		}

		public static Quaternion Average(Quaternion[] quaternions, float[] weights)
		{
			Vector3 forward = Vector3.zero;
			Vector3 up = Vector3.zero;
			float weight = 0f;
			for (int i = 0; i < quaternions.Length; i++)
			{
				forward += quaternions[i] * Vector3.forward * weights[i];
				up += quaternions[i] * Vector3.up * weights[i];
				weight += weights[i];
			}
			if (Mathf.Approximately(weight, 0f))
			{
				return Quaternion.identity;
			}
			return Quaternion.LookRotation(forward / weight, up / weight);
		}

		public static Quaternion SmoothDamp(this Quaternion rot, Quaternion target, ref Quaternion velocity, float smoothTime, float deltaTime)
		{
			if (deltaTime < Mathf.Epsilon) return rot;

			// Account for double-cover.
			var Dot = Quaternion.Dot(rot, target);
			var Multi = Dot > 0f ? 1f : -1f;
			target.x *= Multi;
			target.y *= Multi;
			target.z *= Multi;
			target.w *= Multi;

			// Smooth damp (nlerp approx),
			var Result = new Vector4(
				Mathf.SmoothDamp(rot.x, target.x, ref velocity.x, smoothTime, float.MaxValue, deltaTime),
				Mathf.SmoothDamp(rot.y, target.y, ref velocity.y, smoothTime, float.MaxValue, deltaTime),
				Mathf.SmoothDamp(rot.z, target.z, ref velocity.z, smoothTime, float.MaxValue, deltaTime),
				Mathf.SmoothDamp(rot.w, target.w, ref velocity.w, smoothTime, float.MaxValue, deltaTime)
			).normalized;

			// Ensure velocity is tangent.
			var derivError = Vector4.Project(new Vector4(velocity.x, velocity.y, velocity.z, velocity.w), Result);
			velocity.x -= derivError.x;
			velocity.y -= derivError.y;
			velocity.z -= derivError.z;
			velocity.w -= derivError.w;

			return new Quaternion(Result.x, Result.y, Result.z, Result.w);
		}
	}
}