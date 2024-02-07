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
		#region Send

		/// <summary>
		/// The entity which was hit.
		/// </summary>
		public IHittable Hittable { get; }

		/// <summary>
		/// The entity responsible for this hit.
		/// </summary>
		public IEntity Hitter { get; }

		/// <summary>
		/// The inertia of the hitter.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The swing direction of the hit.
		/// </summary>
		public Vector3 Direction { get; }

		/// <summary>
		/// The total mass behind the hit, defines force transfer capacity.
		/// </summary>
		public float Mass { get; }

		/// <summary>
		/// The total strength behind the hit, defines impact force.
		/// </summary>
		public float Strength { get; }

		/// <summary>
		/// The total offensive power of the hit, defines penetration damage.
		/// </summary>
		public float Offence { get; }

		/// <summary>
		/// The total piercing power of the hit, defines penetration capacity.
		/// </summary>
		public float Piercing { get; }

		#endregion Send

		#region Return

		public float Penetration { get; private set; } = 0f;

		#endregion Return

		public HitData(
			IHittable hittable,
			IEntity hitter,
			Vector3 inertia,
			Vector3 direction,
			float mass,
			float strength,
			float offence,
			float piercing)
		{
			Hittable = hittable;
			Hitter = hitter;
			Inertia = inertia;
			Direction = direction;
			Mass = mass;
			Strength = strength;
			Offence = offence;
			Piercing = piercing;
		}

		public void Return(float penetration)
		{
			Penetration = penetration;
		}
	}
}
