using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Shader data for a ripple: rings spreading from the axis through <see cref="Point"/>
	/// along <see cref="Direction"/>, with the front at <see cref="Radius"/>.
	/// </summary>
	public struct DeformRipple
	{
		public Vector3 Point;
		public Vector3 Direction;
		public float Amplitude;
		public float Radius;
		public float Length;
		public float Decay;
		public float Falloff;
		public float Stretch;
		public float Outward;
		public float Rebound;
	}
}
