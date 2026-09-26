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
		/// Mass of the swung limb+weapon, unblended with the body.
		/// </summary>
		public float LimbMass { get; }

		/// <summary>
		/// Share of the hitter's whole body the strike commits (0 = limb alone, 1 = whole body).
		/// </summary>
		public float BodyMassFraction { get; }

		/// <summary>
		/// The hitter's rank, which sets the mass its strike is expected to carry.
		/// </summary>
		public float Rank { get; }

		/// <summary>
		/// Mass behind the strike: <see cref="LimbMass"/> blended toward <see cref="HitterMass"/> by <see cref="BodyMassFraction"/>.
		/// </summary>
		public float StrikeMass => Mathf.Lerp(LimbMass, HitterMass, BodyMassFraction);

		/// <summary>
		/// The total slashing power of the hit, defines penetration damage.
		/// </summary>
		public float Slash { get; }

		/// <summary>
		/// Total power behind the hit (before mass).
		/// </summary>
		public float Power { get; }

		/// <summary>
		/// Total crit quality behind the hit.
		/// </summary>
		public float Pierce { get; }

		/// <summary>
		/// The whole force behind the hit (body + weapon Power, before the damage-type filter). Drives both
		/// flanks through the other wall and carries blunt, so a pure point still transfers the arm behind it.
		/// </summary>
		public float PowerBand { get; }

		/// <summary>
		/// Body Power plus the weapon's Power share this strike swings; the basis of force.
		/// </summary>
		public float ForceBand { get; }

		/// <summary>
		/// Total luck of the hitter.
		/// </summary>
		public float Luck { get; }

		/// <summary>
		/// The limb's swing speed at contact (1 = normal): a slow, too-heavy swing carries less momentum.
		/// </summary>
		public float SwingSpeed { get; }

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
			float limbMass,
			float bodyMassFraction,
			float rank,
			float slash,
			float power,
			float pierce,
			float powerBand,
			float forceBand,
			float luck,
			float swingSpeed = 1f,
			RuntimeDataCollection data = null)
		{
			SwingSpeed = swingSpeed;
			Receiver = receiver;
			Hitter = hitter;
			HitterMass = hitterMass;
			Inertia = inertia;
			Point = point;
			Direction = direction;
			LimbMass = limbMass;
			BodyMassFraction = Mathf.Clamp01(bodyMassFraction);
			Rank = rank;
			Slash = slash;
			Power = power;
			Pierce = pierce;
			PowerBand = powerBand;
			ForceBand = forceBand;
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
				$"\nLimbMass={LimbMass}," +
				$"\nBodyMassFraction={BodyMassFraction}," +
				$"\nStrikeMass={StrikeMass}," +
				$"\nSlash={Slash}," +
				$"\nPower={Power}," +
				$"\nPierce={Pierce}," +
				$"\nPowerBand={PowerBand}," +
				$"\nForceBand={ForceBand}," +
				$"\nSwingSpeed={SwingSpeed}," +
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
		/// Return data defining whether this hit caused the receiver to be stunned.
		/// </summary>
		public const string STUNNED = "Stunned";
		/// <summary>
		/// Return data defining whether this hit landed as a critical hit.
		/// </summary>
		public const string CRIT = "Crit";
		/// <summary>
		/// Return data defining whether this hit killed the receiver.
		/// </summary>
		public const string KILLED = "Killed";

		// FLOATS
		/// <summary>
		/// Return data: the stagger a parry turned back, drained from the hitter's endurance.
		/// </summary>
		public const string ENDURANCE_RETURN = "Endurance_Return";
		/// <summary>
		/// Return data: parry timing quality, 0 (mistimed, acts as an unguarded hit) to 1 (perfect).
		/// </summary>
		public const string PARRY_QUALITY = "Parry_Quality";
		/// <summary>
		/// Return data defining the guard weight of the receiver during the hit (0=no guard, 1=full guard).
		/// </summary>
		public const string GUARD_WEIGHT = "GuardWeight";
		/// <summary>
		/// Return data defining the amount of coupling.
		/// </summary>
		public const string COUPLING = "Coupling";
		/// <summary>
		/// Return data defining the receiver's max health; the denominator the hitter's output is measured against.
		/// </summary>
		public const string HEALTH_MAX = "Health_Max";
		/// <summary>
		/// Return data defining the amount of added critical damage.
		/// </summary>
		public const string CRIT_DAMAGE = "Crit_Damage";
		/// <summary>
		/// Return data defining the amount of piercing damage, not counting a crit's bonus.
		/// </summary>
		public const string PIERCE_DAMAGE = "Pierce_Damage";
		/// <summary>
		/// Return data defining the percentage of penetration dealt to receiver (0-1~).
		/// </summary>
		public const string PENETRATION = "Penetration";
		/// <summary>
		/// Return data defining the amount of slashing damage.
		/// </summary>
		public const string SLASH_DAMAGE = "Slash_Damage";
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
		/// Return data: how much of the raw offense (Slash+Power+Pierce) actually landed as damage (0-1).
		/// </summary>
		public const string EFFECTIVENESS = "Effectiveness";
		/// <summary>
		/// Return data defining actual amount of damage that has been subtracted from the receiver's health.
		/// </summary>
		public const string DAMAGE_DEALT = "Damage_Dealt";
		/// <summary>
		/// Return data: how much of the blow the guard turned away, as a fraction of the unguarded hit (0-1).
		/// </summary>
		public const string DAMAGE_GUARDED = "Damage_Guarded";
		/// <summary>
		/// Return data: total damage the blow would have dealt unguarded. Only written when a guard was up.
		/// </summary>
		public const string DAMAGE_UNGUARDED = "Damage_Unguarded";
		/// <summary>
		/// The surface type of the armament that struck; empty when the blow was unarmed.
		/// </summary>
		public const string WEAPON_SURFACE = "Weapon_Surface";
		/// <summary>
		/// Written by the hitter: 0-1 share of the strike its momentum still delivered; scales both hit pauses.
		/// </summary>
		public const string CARRY = "Carry";
		/// <summary>
		/// Return data defining total amount of force transfered to receiver.
		/// </summary>
		public const string FORCE = "Force";
		/// <summary>
		/// Return data: the world-space velocity-change the hitter must apply to itself to shed its
		/// share of the closing momentum (inelastic inertia sharing). Applied as a VelocityChange.
		/// </summary>
		public const string INERTIA_BRAKE = "Inertia_Brake";
		/// <summary>
		/// The percentage of endurance-damage that was endured.
		/// Examples:
		///     0 = The endurance was already empty and thus nothing was endured, receiver is stunned.
		///     0.5 = Only half of the force was endured, receiver is stunned.
		///     1 = The full force of the hit was endured by the receiver, receiver is NOT stunned.
		/// </summary>
		public const string ENDURED = "Endured";
		/// <summary>
		/// Written by the hitter: seconds of hit-pause their strike runs, so its audio/FX tail can await the release.
		/// </summary>
		public const string HIT_PAUSE = "Hit_Pause";
		#endregion Return
	}
}
