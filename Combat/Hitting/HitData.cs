using UnityEngine;

namespace SpaxUtils
{
	public struct HitData
	{
		/// <summary>
		/// The entity responsible for this hit.
		/// </summary>
		public IEntity Hitter { get; set; }

		/// <summary>
		/// The velocity with which this hit was performed.
		/// </summary>
		public Vector3 Velocity { get; set; }

		/// <summary>
		/// The mass behind the hit.
		/// </summary>
		public float Mass { get; set; }

		/// <summary>
		/// The (swing) direction of the hit.
		/// </summary>
		public Vector3 Direction { get; set; }
	}
}
