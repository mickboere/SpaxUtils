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
		/// Total mass behind the striking limb+weapon.
		/// </summary>
		public float Mass { get; }

		/// <summary>
		/// The total piercing power of the hit, defines penetration damage.
		/// </summary>
		public float Piercing { get; }

		/// <summary>
		/// Total power behind the hit (before mass).
		/// </summary>
		public float Power { get; }

		/// <summary>
		/// Total crit quality behind the hit.
		/// </summary>
		public float Precision { get; }

		/// <summary>
		/// Total luck of the hitter.
		/// </summary>
		public float Luck { get; }

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
			float piercing,
			float power,
			float precision,
			float luck,
			RuntimeDataCollection data = null)
		{
			Receiver = receiver;
			Hitter = hitter;
			HitterMass = hitterMass;
			Inertia = inertia;
			Point = point;
			Direction = direction;
			Mass = mass;
			Piercing = piercing;
			Power = power;
			Precision = precision;
			Luck = luck;
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
				$"\nPiercing={Piercing}," +
				$"\nPower={Power}," +
				$"\nPrecision={Precision}," +
				$"\n\nData:\n{Data},";
		}
	}

	public class HitDataIdentifiers
	{
		#region Return

		// BOOLS
		/// <summary>
		/// Return data defining whether this hit was perfectly blocked.
		/// </summary>
		public const string BLOCKED = "Blocked";
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
		/// <summary>
		/// Return data defining whether this hit landed as a critical hit.
		/// </summary>
		public const string CRIT = "Crit";

		// FLOATS
		/// <summary>
		/// Return data defining the amount of guard the receiver had during the hit (0=no guard, 1=full guard).
		/// </summary>
		public const string GUARD = "Guard";
		/// <summary>
		/// Return data defining the amount of coupling.
		/// </summary>
		public const string COUPLING = "Coupling";
		/// <summary>
		/// Return data defining the amount of added critical damage.
		/// </summary>
		public const string CRIT_DAMAGE = "Crit_Damage";
		/// <summary>
		/// Return data defining the percentage of penetration dealt to receiver (0-1~).
		/// </summary>
		public const string PENETRATION = "Penetration";
		/// <summary>
		/// Return data defining the amount of piercing damage.
		/// </summary>
		public const string PIERCING_DAMAGE = "Piercing_Damage";
		/// <summary>
		/// Return data defining percentage of impact dealt to receiver (0-1~).
		/// </summary>
		public const string IMPACT = "Impact";
		/// <summary>
		/// Return data defining the amount of blunt damage.
		/// </summary>
		public const string BLUNT_DAMAGE = "Blunt_Damage";
		/// <summary>
		/// Return data defining the amount of grace intervention that was subtracted from total damage.
		/// </summary>
		public const string GRACE = "Grace";
		/// <summary>
		/// Return data defining total amount of damage that will be dealt to the receiver.
		/// </summary>
		public const string DAMAGE_TOTAL = "Damage_Total";
		/// <summary>
		/// Return data defining actual amount of damage that has been subtracted from the receiver's health.
		/// </summary>
		public const string DAMAGE_DEALT = "Damage_Dealt";
		/// <summary>
		/// Return data defining total amount of force transfered to receiver.
		/// </summary>
		public const string FORCE = "Force";
		/// <summary>
		/// The percentage of endurance-damage that was endured.
		/// Examples:
		///     0 = The endurance was already empty and thus nothing was endured, receiver is stunned.
		///     0.5 = Only half of the force was endured, receiver is stunned.
		///     1 = The full force of the hit was endured by the receiver, receiver is NOT stunned.
		/// </summary>
		public const string ENDURED = "Endured";
		#endregion Return
	}
}
