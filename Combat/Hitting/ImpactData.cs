using UnityEngine;

namespace SpaxUtils
{
	public struct ImpactData
	{
		public Vector3 Point { get; set; }
		public Vector3 Momentum { get; set; }
		public float Force { get; set; }

		public ImpactData(Vector3 point, Vector3 momentum, float force)
		{
			Point = point;
			Momentum = momentum;
			Force = force;
		}
	}
}
