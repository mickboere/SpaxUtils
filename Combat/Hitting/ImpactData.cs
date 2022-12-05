using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Impact data containing impact point, direction and force.
	/// </summary>
	public struct ImpactData
	{
		public Vector3 Point { get; set; }
		public Vector3 Direction { get; set; }
		public float Force { get; set; }

		public ImpactData(Vector3 point, Vector3 direction, float force)
		{
			Point = point;
			Direction = direction;
			Force = force;
		}
	}
}
