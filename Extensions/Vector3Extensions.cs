using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public static class Vector3Extensions
	{
		#region In-Line Flattening

		/// <summary>
		/// Handy method to flatten the X axis in-line.
		/// </summary>
		public static Vector3 FlattenX(this Vector3 vector3)
		{
			vector3.x = 0f;
			return vector3;
		}

		/// <summary>
		/// Handy method to flatten the Y axis in-line.
		/// </summary>
		public static Vector3 FlattenY(this Vector3 vector3)
		{
			vector3.y = 0f;
			return vector3;
		}

		/// <summary>
		/// Handy method to flatten the Z axis in-line.
		/// </summary>
		public static Vector3 FlattenZ(this Vector3 vector3)
		{
			vector3.z = 0f;
			return vector3;
		}

		public static Vector3 FlattenXY(this Vector3 vector3)
		{
			vector3.x = 0f;
			vector3.y = 0f;
			return vector3;
		}

		public static Vector3 FlattenYZ(this Vector3 vector3)
		{
			vector3.y = 0f;
			vector3.z = 0f;
			return vector3;
		}

		public static Vector3 FlattenXZ(this Vector3 vector3)
		{
			vector3.x = 0f;
			vector3.z = 0f;
			return vector3;
		}

		#endregion In-Line Flattening

		#region In-Line Setting

		/// <summary>
		/// Handy method to set the X axis in-line.
		/// </summary>
		public static Vector3 SetX(this Vector3 vector3, float x)
		{
			vector3.x = x;
			return vector3;
		}

		/// <summary>
		/// Handy method to set the Y axis in-line.
		/// </summary>
		public static Vector3 SetY(this Vector3 vector3, float y)
		{
			vector3.y = y;
			return vector3;
		}

		/// <summary>
		/// Handy method to set the Z axis in-line.
		/// </summary>
		public static Vector3 SetZ(this Vector3 vector3, float z)
		{
			vector3.z = z;
			return vector3;
		}
		#endregion In-Line Setting

		#region In-Line Mirroring
		/// <summary>
		/// Handy method to mirror the X axis in-line.
		/// </summary>
		public static Vector3 MirrorX(this Vector3 vector3)
		{
			vector3.x = -vector3.x;
			return vector3;
		}

		/// <summary>
		/// Handy method to mirror the Y axis in-line.
		/// </summary>
		public static Vector3 MirrorY(this Vector3 vector3)
		{
			vector3.y = -vector3.y;
			return vector3;
		}

		/// <summary>
		/// Handy method to mirror the Z axis in-line.
		/// </summary>
		public static Vector3 MirrorZ(this Vector3 vector3)
		{
			vector3.z = -vector3.z;
			return vector3;
		}
		#endregion In-Line Mirroring

		#region In-Line Multiplying

		/// <summary>
		/// Handy method to multiply the X axis in-line.
		/// </summary>
		public static Vector3 MultX(this Vector3 vector3, float m)
		{
			vector3.x *= m;
			return vector3;
		}

		/// <summary>
		/// Handy method to multiply the Y axis in-line.
		/// </summary>
		public static Vector3 MultY(this Vector3 vector3, float m)
		{
			vector3.y *= m;
			return vector3;
		}

		/// <summary>
		/// Handy method to multiply the Z axis in-line.
		/// </summary>
		public static Vector3 MultZ(this Vector3 vector3, float m)
		{
			vector3.z *= m;
			return vector3;
		}
		#endregion In-Line Multiplying

		/// <summary>
		/// Converts a <see cref="Vector3"/> direction to a <see cref="Quaternion"/> rotation.
		/// </summary>
		public static Quaternion LookRotation(this Vector3 vector3, Vector3 up)
		{
			return Quaternion.LookRotation(vector3, up);
		}

		/// <summary>
		/// Converts a <see cref="Vector3"/> direction to a <see cref="Vector3"/> rotation euler angles.
		/// </summary>
		public static Vector3 LookEulerAngles(this Vector3 vector3, Vector3 up)
		{
			return vector3.LookRotation(up).eulerAngles;
		}

		/// <summary>
		/// Converts a <see cref="Vector3"/> rotation euler angles to a <see cref="Vector3"/> direction.
		/// </summary>
		public static Vector3 LookEuler(this Vector3 eulerAngles)
		{
			return Quaternion.Euler(eulerAngles) * Vector3.forward;
		}

		/// <summary>
		/// Have <paramref name="vector"/> look in <paramref name="forward"/>.
		/// </summary>
		public static Vector3 Look(this Vector3 vector, Vector3 forward)
		{
			return Quaternion.LookRotation(forward) * vector;
		}

		/// <summary>
		/// Have <paramref name="vector"/> look in <paramref name="forward"/>.
		/// </summary>
		public static Vector3 Look(this Vector3 vector, Vector3 forward, Vector3 up)
		{
			return Quaternion.LookRotation(forward, up) * vector;
		}

		/// <summary>
		/// Treats the <see cref="Vector3"/> as a rotatable direction and clamps its rotation to the <paramref name="maxAngle"/> along the <paramref name="axis"/>.
		/// </summary>
		public static Vector3 ClampDirection(this Vector3 direction, Vector3 axis, float maxAngle)
		{
			float angle = Vector3.Angle(direction, axis);
			if (angle > maxAngle)
			{
				direction = Quaternion.Lerp(Quaternion.LookRotation(direction), Quaternion.LookRotation(axis), 1f - (maxAngle / angle)) * Vector3.forward;
			}
			return direction;
		}

		public static Vector3 ClampMagnitude(this Vector3 vector, float min, float max)
		{
			float magnitude = vector.magnitude;
			float diff = Mathf.Clamp(magnitude, min, max) - magnitude;
			vector -= vector.normalized * diff;
			return vector;
		}

		public static Vector3 ClampMagnitude(this Vector3 vector, float max)
		{
			vector = Vector3.ClampMagnitude(vector, max);
			return vector;
		}

		/// <summary>
		/// Projects <paramref name="vector"/> onto plane with <paramref name="normal"/>.
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="normal"></param>
		/// <returns></returns>
		public static Vector3 ProjectOnPlane(this Vector3 vector, Vector3 normal)
		{
			return Vector3.ProjectOnPlane(vector, normal);
		}

		/// <summary>
		/// Projects <paramref name="vector"/> onto a plane with <paramref name="normal"/> and returns the difference.
		/// </summary>
		public static Vector3 DisperseOnPlane(this Vector3 vector, Vector3 normal)
		{
			return Vector3.ProjectOnPlane(vector, normal) - vector;
		}

		/// <summary>
		/// Returns the largest axis value.
		/// </summary>
		public static float Max(this Vector3 vector3)
		{
			return Mathf.Max(vector3.x, vector3.y, vector3.z);
		}

		/// <summary>
		/// Projects <paramref name="a"/>-><paramref name="v"/> onto <paramref name="a"/>-><paramref name="b"/> 
		/// to give the Vector3 inverse-lerp value as it would work in <see cref="Mathf.InverseLerp(float, float, float)"/>.
		/// </summary>
		public static Vector3 InverseLerp(Vector3 a, Vector3 b, Vector3 v)
		{
			Vector3 ab = b - a;
			Vector3 av = v - a;
			return Vector3.Project(av, ab);
		}

		/// <summary>
		/// Projects <paramref name="a"/>-><paramref name="v"/> onto <paramref name="a"/>-><paramref name="b"/> 
		/// to give the Vector3 inverse-lerp value as it would work in <see cref="Mathf.InverseLerp(float, float, float)"/>.
		/// </summary>
		public static Vector3 InverseLerp(Vector3 a, Vector3 b, Vector3 v, out float value)
		{
			Vector3 ab = b - a;
			Vector3 av = v - a;
			Vector3 projection = Vector3.Project(av, ab);
			value = projection.magnitude / ab.magnitude;
			return projection;
		}

		/// <summary>
		/// Compute barycentric coordinates for point p with respect to triangle (a, b, c).
		/// https://gamedev.stackexchange.com/questions/23743/whats-the-most-efficient-way-to-find-barycentric-coordinates
		/// </summary>
		public static Vector3 Barycentric(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
		{
			Vector3 ab = b - a;
			Vector3 ac = c - a;
			Vector3 ap = p - a;

			float d00 = Vector3.Dot(ab, ab);
			float d01 = Vector3.Dot(ab, ac);
			float d11 = Vector3.Dot(ac, ac);
			float d20 = Vector3.Dot(ap, ab);
			float d21 = Vector3.Dot(ap, ac);
			float denom = d00 * d11 - d01 * d01;

			float v = (d11 * d20 - d01 * d21) / denom;
			float w = (d00 * d21 - d01 * d20) / denom;
			float u = 1.0f - v - w;

			return new Vector3(u, v, w);
		}

		public static Vector3 Lerp(this Vector3 a, Vector3 b, float t)
		{
			return Vector3.Lerp(a, b, t);
		}

		public static float Dot(this Vector3 a, Vector3 b)
		{
			return Vector3.Dot(a, b);
		}

		/// <summary>
		/// Returns normalized dot product, remapping from (-1,1) to (0,1), where -1=0, 0=0.5, 1=1.
		/// </summary>
		public static float NormalizedDot(this Vector3 a, Vector3 b)
		{
			return (Vector3.Dot(a, b) + 1) * 0.5f;
		}

		/// <summary>
		/// Returns clamped dot product, remapping from (-1,1) to (0,1) where -1=0, 0=0, 1=1.
		/// </summary>
		public static float ClampedDot(this Vector3 a, Vector3 b)
		{
			return Mathf.Clamp01(Vector3.Dot(a, b));
		}

		/// <summary>
		/// Returns absolute dot product, remapping from (-1,1) to (0,1) where -1=1, 0=0, 1=1.
		/// </summary>
		public static float AbsoluteDot(this Vector3 a, Vector3 b)
		{
			return Mathf.Abs(Vector3.Dot(a, b));
		}

		public static Vector3 Cross(this Vector3 a, Vector3 b)
		{
			return Vector3.Cross(a, b);
		}

		public static float Distance(this Vector3 a, Vector3 b)
		{
			return (b - a).magnitude;
		}

		public static Vector3 Add(this Vector3 a, Vector3 b)
		{
			return a + b;
		}

		public static Vector3 Multiply(this Vector3 vector, Vector3 scale)
		{
			return new Vector3(vector.x * scale.x, vector.y * scale.y, vector.z * scale.z);
		}

		public static Vector3 Multiply(this Vector3 vector, float scale)
		{
			return vector * scale;
		}

		public static Vector3 Divide(this Vector3 a, Vector3 b)
		{
			return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
		}

		#region Transformation

		/// <summary>
		/// Converts global vector to local vector (InverseTransformDirection).
		/// </summary>
		public static Vector3 Localize(this Vector3 vector, Transform transform)
		{
			return transform.InverseTransformDirection(vector);
		}

		/// <summary>
		/// Converts local vector to global vector (by multiplying the vector by the transform's rotation).
		/// </summary>
		/// <param name="vector"></param>
		/// <param name="transform"></param>
		/// <returns></returns>
		public static Vector3 Globalize(this Vector3 vector, Transform transform)
		{
			return transform.rotation * vector;
		}

		/// <summary>
		/// Sample point on circle at <paramref name="t"/> from <paramref name="centre"/> pointing in <paramref name="forward"/>.
		/// </summary>
		public static Vector3 Circle(this Vector3 centre, float radius, float t, Vector3 forward)
		{
			return centre + new Vector3(Mathf.Sin(t * Mathf.PI * 2f), Mathf.Cos(t * Mathf.PI * 2f), 0f).Multiply(radius).Look(forward);
		}

		/// <summary>
		/// Sample point on circle at <paramref name="t"/> from <paramref name="centre"/> pointing in <paramref name="forward"/>.
		/// </summary>
		public static Vector3 Circle(this Vector3 centre, float radius, float t, Vector3 forward, Vector3 up)
		{
			return centre + new Vector3(Mathf.Sin(t * Mathf.PI * 2f), Mathf.Cos(t * Mathf.PI * 2f), 0f).Multiply(radius).Look(forward, up);
		}

		/// <summary>
		/// Sample point on circle at <paramref name="t"/> from <paramref name="centre"/> pointing in <paramref name="forward"/>.
		/// </summary>
		public static Vector3 Circle(this Vector3 centre, Vector2 scale, float t, Vector3 forward, Vector3 up)
		{
			return centre + new Vector3(Mathf.Sin(t * Mathf.PI * 2f), Mathf.Cos(t * Mathf.PI * 2f), 0f).Multiply(scale).Look(forward, up);
		}

		#endregion

		#region Averaging

		/// <summary>
		/// Returns the average length of the vector components.
		/// </summary>
		public static float Average(this Vector3 v3)
		{
			return (Mathf.Abs(v3.x) + Mathf.Abs(v3.y) + Mathf.Abs(v3.z)) / 3f;
		}

		public static Vector3 AveragePoint(params Vector3[] vectors)
		{
			Vector3 total = Vector3.zero;
			foreach (Vector3 v in vectors)
			{
				total += v;
			}
			return total / vectors.Length;
		}

		public static Vector3 AveragePoint(this IReadOnlyCollection<Vector3> vectors)
		{
			Vector3 total = Vector3.zero;
			foreach (Vector3 v in vectors)
			{
				total += v;
			}
			return total / vectors.Count;
		}

		public static Vector3 AveragePoint(Vector3[] vectors, float[] weights)
		{
			Vector3 total = Vector3.zero;
			float weight = 0f;
			for (int i = 0; i < vectors.Length; i++)
			{
				total += vectors[i] * weights[i];
				weight += weights[i];
			}
			return total / weight;
		}

		public static Vector3 AveragePoint(Vector3 center, Vector3[] vectors)
		{
			Vector3 total = Vector3.zero;
			foreach (Vector3 v in vectors)
			{
				total += v - center;
			}
			return total / vectors.Length + center;
		}

		public static Vector3 AveragePoint(Vector3 center, Vector3[] vectors, float[] weights)
		{
			Vector3 total = Vector3.zero;
			float weight = 0f;
			for (int i = 0; i < vectors.Length; i++)
			{
				total += (vectors[i] - center) * weights[i];
				weight += weights[i];
			}
			if (Mathf.Approximately(weight, 0f))
			{
				return center;
			}
			return total / weight + center;
		}

		public static Vector3 AverageDirection(this IReadOnlyCollection<Vector3> vectors)
		{
			Vector3 total = Vector3.zero;
			foreach (Vector3 v in vectors)
			{
				total += v;
			}
			return total.normalized;
		}

		#endregion // Averaging

		#region OnLine

		public static Vector3 ClosestOnLine(this Vector3 point, Vector3 a, Vector3 b)
		{
			Vector3 direction = b - a;
			float length = direction.magnitude;
			direction.Normalize();
			float projectionLength = Mathf.Clamp(Vector3.Dot(point - a, direction), 0f, length);
			return a + direction * projectionLength;
		}

		public static Vector3 ClosestOnInfiniteLine(this Vector3 point, Vector3 a, Vector3 b)
		{
			return a + Vector3.Project(point - a, b - a);
		}

		#endregion OnLine

		#region Physics

		/// <summary>
		/// Calculates the force required to go from <paramref name="current"/>(this) to <paramref name="target"/> velocity.
		/// </summary>
		/// <param name="current">Current velocity.</param>
		/// <param name="target">Target velocity.</param>
		/// <param name="power">The amount of power we are allowed to use to reach the target velocity, a higher power will feel more immediate but has the possibility to overshoot.</param>
		/// <returns>The force required to go from <paramref name="current"/>(this) to <paramref name="target"/> velocity.</returns>
		public static Vector3 CalculateForce(this Vector3 current, Vector3 target, float power = 1f)
		{
			return ((target - current) / Time.fixedDeltaTime * power);
		}

		/// <summary>
		/// Calculates the force required to go from <paramref name="current"/>(this) to <paramref name="target"/> velocity,
		/// clamping the result to <paramref name="maxForce"/>.
		/// </summary>
		/// <param name="current">Current velocity.</param>
		/// <param name="target">Target velocity.</param>
		/// <param name="power">The amount of power we are allowed to use to reach the target velocity, a higher power will feel more immediate but has the possibility to overshoot.</param>
		/// <param name="maxForce">The value to clamp the force magnitude to.</param>
		/// <returns></returns>
		public static Vector3 CalculateForce(this Vector3 current, Vector3 target, float power, float maxForce)
		{
			return current.CalculateForce(target, power).ClampMagnitude(maxForce);
		}

		/// <summary>
		/// Calculates kinetic energy of a body.
		/// </summary>
		public static Vector3 KineticEnergy(this Vector3 velocity, float mass)
		{
			return 0.5f * mass * velocity * velocity.magnitude;
		}

		#endregion // Physics

		/// <summary>
		/// Approximate a normal vector from a collection of points by comparing the highest and the lowest.
		/// </summary>
		public static Vector3 ApproxNormalFromPoints(this Vector3[] points, Vector3 up, out Vector3 center, bool debug = false, float debugSize = 1f)
		{
			center = AveragePoint(points);

			// Collect normals from center to all points in one vector.
			Vector3 normal = default;
			for (int i = 0; i < points.Length; i++)
			{
				// int next = i.AddAndRepeat(1, points.Length); <- wtf?
				int next = Mathf.Min(i + 1, points.Length - 1);

				Vector3 a = center - points[i];
				Vector3 b = points[next] - points[i];
				Vector3 n = a.Cross(b).normalized;
				n *= Vector3.Dot(n, up) < 0f ? -1f : 1f;
				normal += n;
#if UNITY_EDITOR
				if (debug)
				{
					Debug.DrawRay(points[i], a.normalized * debugSize, Color.red);
					Debug.DrawRay(points[i], b.normalized * debugSize, Color.green);
					Debug.DrawRay(points[i], n * debugSize, Color.cyan);
				}
#endif
			}

			// Normalize result and done.
			return normal.normalized;
		}

		/// <summary>
		/// Will apply <paramref name="curve"/> to <paramref name="vector"/>, treating it as a directional vector.
		/// </summary>
		/// <param name="vector">Directional vector to apply the curve to.</param>
		/// <param name="curve">Curve to apply to the vector.</param>
		/// <param name="max">Vector magnitude corresponding to an evaluation of t=1 on the curve.</param>
		/// <returns></returns>
		public static Vector3 ApplyCurve(this Vector3 vector, AnimationCurve curve, float max = 1f)
		{
			float length = vector.magnitude;
			float evaluation = curve.Evaluate(length / max);
			return vector.normalized * evaluation;
		}
	}
}
