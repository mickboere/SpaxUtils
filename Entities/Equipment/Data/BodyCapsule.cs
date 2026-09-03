using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// One of the body's own collision capsules, reduced to the segment between its end-sphere centres
	/// and a radius, in the torso's frame. Authored on the rig, so it is the shape actually seen.
	/// </summary>
	public struct BodyCapsule
	{
		public Vector3 Start;
		public Vector3 End;
		public float Radius;

		/// <summary>The point on the capsule's axis nearest <paramref name="local"/>.</summary>
		public Vector3 Closest(Vector3 local)
		{
			Vector3 along = End - Start;
			float length = along.sqrMagnitude;
			if (length < 0.000001f)
			{
				return Start;
			}

			return Start + along * Mathf.Clamp01(Vector3.Dot(local - Start, along) / length);
		}

		public override string ToString()
		{
			return $"{Start}-{End} r {Radius:0.###}";
		}
	}
}
