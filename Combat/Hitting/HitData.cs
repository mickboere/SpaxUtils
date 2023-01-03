using UnityEngine;

namespace SpaxUtils
{
	public struct HitData
	{
		public IEntity Hitter { get; set; }
		public Vector3 Momentum { get; set; }
		public float Mass { get; set; }
	}
}
