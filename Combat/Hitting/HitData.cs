using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class created for each hit during an attack.
	/// Hit entities can edit data for the hitter to read back.
	/// </summary>
	public class HitData
	{
		/// <summary>
		/// The entity responsible for this hit.
		/// </summary>
		public IEntity Hitter { get; set; }

		/// <summary>
		/// The inertia of the hitter.
		/// </summary>
		public Vector3 Inertia { get; set; }

		/// <summary>
		/// The total force behind the hit.
		/// </summary>
		public float Force { get; set; }

		/// <summary>
		/// The (swing) direction of the hit.
		/// </summary>
		public Vector3 Direction { get; set; }
	}
}
