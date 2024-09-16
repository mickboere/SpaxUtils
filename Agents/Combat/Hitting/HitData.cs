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
		public IHittable Receiver { get; }

		/// <summary>
		/// The entity responsible for initiating this hit.
		/// </summary>
		public IEntity Hitter { get; }

		/// <summary>
		/// The total mass of the hitter, used to apply inertia.
		/// </summary>
		public float Mass { get; }

		/// <summary>
		/// The inertia of the hitter in world-space.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The hit-point in world space.
		/// </summary>
		public Vector3 Point { get; }

		/// <summary>
		/// The normalized inbound direction of the hit in world space.
		/// </summary>
		public Vector3 Direction { get; }

		/// <summary>
		/// The total force behind the hit, defines force transfer capacity.
		/// </summary>
		public float Force { get; }

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
		/// Return data defining whether the receiver was guarding/blocking against the attack and the amound of guard weight applied to the hit.
		/// </summary>
		public float Result_Blocked { get; set; }

		/// <summary>
		/// Return data defining whether this hit was parried by the receiver.
		/// </summary>
		public bool Result_Parried { get; set; }

		/// <summary>
		/// Return data defining whether this hit caused the receiver to be stunned.
		/// </summary>
		public bool Result_Stunned { get; set; }

		/// <summary>
		/// Return data defining the percentage of penetration dealt to receiver (0-1~).
		/// </summary>
		public float Result_Penetration { get; set; }

		/// <summary>
		/// Return data defining percentage of impact dealt to receiver (0-1~).
		/// </summary>
		public float Result_Impact { get; set; }

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
			IHittable receiver,
			IEntity hitter,
			float mass,
			Vector3 inertia,
			Vector3 point,
			Vector3 direction,
			float force,
			float strength = 0f,
			float offence = 0f,
			float piercing = 0f)
		{
			Receiver = receiver;
			Hitter = hitter;
			Mass = mass;
			Inertia = inertia;
			Point = point;
			Direction = direction;
			Force = force;
			Strength = strength;
			Offence = offence;
			Piercing = piercing;
		}

		public override string ToString()
		{
			return $"HitData:" +
				$"\nReceiver={Receiver.Entity.Identification.TagFull()}," +
				$"\nHitter={Hitter.Identification.TagFull()}," +
				$"\nMass={Mass}," +
				$"\nInertia={Inertia}," +
				$"\nPoint={Point}," +
				$"\nDirection={Direction}," +
				$"\nForce={Force}," +
				$"\nStrength={Strength}," +
				$"\nOffence={Offence}," +
				$"\nPiercing={Piercing}," +
				$"\n\nResult_Blocked={Result_Blocked}," +
				$"\nResult_Parried={Result_Parried}," +
				$"\nResult_Stunned={Result_Stunned}," +
				$"\nResult_Penetration={Result_Penetration}," +
				$"\nResult_Impact={Result_Impact}," +
				$"\nResult_Damage={Result_Damage}," +
				$"\nResult_Force={Result_Force}";
		}
	}
}
