using System.Collections.Generic;
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
		public IEntity Hitter { get; }

		/// <summary>
		/// The entity which was hit.
		/// </summary>
		public IHittable Hittable { get; }

		/// <summary>
		/// The inertia of the hitter.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The total force behind the hit.
		/// Can differ from inertia to impose shounen-knockback.
		/// </summary>
		public float Force { get; }

		/// <summary>
		/// The direction of the hit.
		/// </summary>
		public Vector3 HitDirection { get; }

		/// <summary>
		/// All damage values to be processed.
		/// </summary>
		public Dictionary<string, float> Damages { get; }

		public HitData(
			IEntity hitter,
			IHittable hittable,
			Vector3 inertia,
			float force,
			Vector3 hitDirection,
			Dictionary<string, float> damages)
		{
			Hitter = hitter;
			Hittable = hittable;
			Inertia = inertia;
			Force = force;
			HitDirection = hitDirection;
			Damages = damages;
		}
	}
}
