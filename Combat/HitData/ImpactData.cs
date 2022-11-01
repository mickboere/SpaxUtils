using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Impact data containing impact point, direction of impact, and the impact force.
	/// </summary>
	public class ImpactData
	{
		public Vector3 Point { get; set; }
		public Vector3 Direction { get; set; }
		public float Force { get; set; }

		public ImpactData(Vector3 hitPoint, Vector3 direction, float force)
		{
			Point = hitPoint;
			Direction = direction;
			Force = force;
		}
	}
}