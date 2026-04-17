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
		MeleeAttackDirection AttackDirection { get; }

		/// <summary>
		/// Collection of hit-box bone identifiers for this move's performance.
		/// </summary>
		List<string> HitBoxes { get; }

		/// <summary>
		/// Time into combat performance before hit detection is activated.
		/// </summary>
		float HitDetectionDelay { get; }

		/// <summary>
		/// Whether the attack has a custom hit-direction.
		/// When FALSE, the default calculated direction will be passed into the hit.
		/// </summary>
		bool CustomDirection { get; }

		/// <summary>
		/// The custom relative hit-direction to use when <see cref="CustomDirection"/> is TRUE.
		/// Does not need to be a normalized value, can exceed power capacity.
		/// </summary>
		Vector3 HitDirection { get; }

		#region Momentum

		/// <summary>
		/// The relative inertia of this combat move to apply to the user.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The delay in applied inertia into the performance runtime.
		/// </summary>
		public float InertiaDelay { get; }

		/// <summary>
		/// The (maximum) traversable distance when performing a charged attack.
		/// </summary>
		public float StormDistance { get; }

		/// <summary>
		/// Whether the charge pose should be held until done applying momentum.
		/// </summary>
		public bool PrelongCharge { get; }

		/// <summary>
		/// The velocity above which the performance should be prolonged.
		/// </summary>
		public float ProlongThreshold { get; }

		#endregion Momentum

		#region Stats

		/// <summary>
		/// Sub-Stat identifying the limb responsible for performing hits.
		/// </summary>
		string Limb { get; }

		/// <summary>
		/// Percentage of user's Piercing that gets transfered into the attack.
		/// </summary>
		float Piercing { get; }

		/// <summary>
		/// Percentage of user's Power that gets transfered into the attack.
		/// </summary>
		float Power { get; }

		/// <summary>
		/// Percentage of user's Precision that gets transfered into the attack.
		/// </summary>
		float Precision { get; }

		/// <summary>
		/// How much balance is maintained while charging.
		/// </summary>
		float ChargeBalance { get; }

		/// <summary>
		/// How much balance is maintained while performing.
		/// </summary>
		float PerformBalance { get; }

		#endregion Stats
	}
}
