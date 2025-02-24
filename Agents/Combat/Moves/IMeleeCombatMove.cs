using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part sequenced melee combat moves (charge, perform, release).
	/// </summary>
	public interface IMeleeCombatMove : ICombatMove
	{
		/// <summary>
		/// The type of attack pattern this combat move utilizes.
		///		- Horizontal: Best way to avoid is moving backwards.
		///		- Vertical: Best way to avoid is moving to the side.
		/// </summary>
		MeleeAttackType AttackType { get; }

		/// <summary>
		/// Collection of hit-box bone identifiers for this move's performance.
		/// </summary>
		List<string> HitBoxes { get; }

		/// <summary>
		/// Time into combat performance before hit detection is activated.
		/// </summary>
		float HitDetectionDelay { get; }

		/// <summary>
		/// The relative inertia of this combat move to apply to the user.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The delay in applied inertia into the performance runtime.
		/// </summary>
		public float InertiaDelay { get; }

		/// <summary>
		/// The velocity above which the performance should be prolonged.
		/// </summary>
		public float ProlongThreshold { get; }

		#region Stats

		/// <summary>
		/// Sub-Stat identifying the limb responsible for performing hits.
		/// </summary>
		string Limb { get; }

		/// <summary>
		/// Percentage of user's strength that gets transfered into the attack.
		/// </summary>
		float Power { get; }

		/// <summary>
		/// Percentage of user's offence that gets transfered into the attack.
		/// </summary>
		float Offence { get; }

		#endregion Stats
	}
}
