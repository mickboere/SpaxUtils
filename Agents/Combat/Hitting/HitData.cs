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
		public float HitterMass { get; }

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
		/// The total mass behind the hit itself.
		/// </summary>
		public float Mass { get; }

		/// <summary>
		/// The total power behind the hit.
		/// </summary>
		public float Power { get; }

		/// <summary>
		/// The total offensive power of the hit, defines penetration damage.
		/// </summary>
		public float Offence { get; }

		/// <summary>
		/// Runtime data container used to store additional hit data.
		/// </summary>
		public RuntimeDataCollection Data;

		public HitData(
			IHittable receiver,
			IEntity hitter,
			float hitterMass,
			Vector3 inertia,
			Vector3 point,
			Vector3 direction,
			float mass,
			float power,
			float offence,
			RuntimeDataCollection data = null)
		{
			Receiver = receiver;
			Hitter = hitter;
			HitterMass = hitterMass;
			Inertia = inertia;
			Point = point;
			Direction = direction;
			Mass = mass;
			Power = power;
			Offence = offence;
			Data = data ?? new RuntimeDataCollection(null);
		}

		public override string ToString()
		{
			return $"HitData:" +
				$"\nReceiver={Receiver.Entity.Identification.TagFull()}," +
				$"\nHitter={Hitter.Identification.TagFull()}," +
				$"\nHitterMass={HitterMass}," +
				$"\nInertia={Inertia}," +
				$"\nPoint={Point}," +
				$"\nDirection={Direction}," +
				$"\nMass={Mass}," +
				$"\nPower={Power}," +
				$"\nOffence={Offence}," +
				$"\n\nData:\n{Data},";
		}
	}

	public class HitDataIdentifiers
	{
		#region Return
		// BOOLS
		/// <summary>
		/// Return data defining whether this hit was parried by the receiver.
		/// </summary>
		public const string PARRIED = "Parried";
		/// <summary>
		/// Return data defining whether this hit was deflected by the receiver.
		/// </summary>
		public const string DEFLECTED = "Deflected";
		/// <summary>
		/// Return data defining whether this hit caused the receiver to be stunned.
		/// </summary>
		public const string STUNNED = "Stunned";

		// FLOATS
		/// <summary>
		/// Return data defining whether the receiver was guarding/blocking against the attack and the amound of guard weight applied to the hit.
		/// </summary>
		public const string BLOCKED = "Blocked";
		/// <summary>
		/// The percentage of endurance-damage that was endured.
		/// Examples:
		///		0 =	The endurance was already empty and thus nothing was endured, receiver is stunned.
		///		0.5 = Only half of the force was endured, receiver is stunned.
		///		1 = The full force of the hit was endured by the receiver, receiver is NOT stunned.
		/// </summary>
		public const string ENDURED = "Endured";
		/// <summary>
		/// Return data defining the percentage of penetration dealt to receiver (0-1~).
		/// </summary>
		public const string PENETRATION = "Penetration";
		/// <summary>
		/// Return data defining percentage of impact dealt to receiver (0-1~).
		/// </summary>
		public const string IMPACT = "Impact";
		/// <summary>
		/// Return data defining total amount of damage dealt to receiver.
		/// </summary>
		public const string DAMAGE = "Damage";
		/// <summary>
		/// Return data defining total amount of force transfered to receiver.
		/// </summary>
		public const string FORCE = "Force";
		#endregion Return
	}
}
