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
		/// The entity responsible for initiating this hit.
		/// </summary>
		public IEntity Hitter { get; }

		/// <summary>
		/// The mass if the hitter.
		/// </summary>
		public float HitterMass { get; }

		/// <summary>
		/// The inertia of the hitter in world-space.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The normalized inbound direction of the hit in world space.
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

		/// <summary>
		/// Return data defining whether this hit was parried by the receiver.
		/// </summary>
		public bool Result_Parried { get; set; }

		/// <summary>
		/// Return data defining total percentage of offensive penetration dealt to receiver.
		/// </summary>
		public float Result_Penetration { get; set; }

		/// <summary>
		/// Return data defining total amount of damage dealt to receiver.
		/// </summary>
		public float Result_Damage { get; set; }

		/// <summary>
		/// Return data defining total amount of force transfered to receiver.
		/// </summary>
		public float Result_Force { get; set; }

		#endregion Return

		public HitData(
			IHittable hittable,
			IEntity hitter,
			float hitterMass,
			Vector3 inertia,
			Vector3 direction,
			float mass,
			float strength,
			float offence,
			float piercing)
		{
			Hittable = hittable;
			Hitter = hitter;
			HitterMass = hitterMass;
			Inertia = inertia;
			Direction = direction;
			Mass = mass;
			Strength = strength;
			Offence = offence;
			Piercing = piercing;
		}
	}
}
