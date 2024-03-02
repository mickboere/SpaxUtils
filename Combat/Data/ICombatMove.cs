using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part combat moves (charge, perform, release).
	/// Implements <see cref="IPerformanceMove"/>.
	/// </summary>
	public interface ICombatMove : IPerformanceMove
	{
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
		/// The delay in applied momentum force into the performance.
		/// </summary>
		public float ForceDelay { get; }

		#region Hit Data

		/// <summary>
		/// Sub-Stat identifying the limb responsible for performing hits.
		/// </summary>
		string Limb { get; }

		/// <summary>
		/// Percentage of user's strength that gets transfered into the attack.
		/// </summary>
		float Strength { get; }

		/// <summary>
		/// Percentage of user's offence that gets transfered into the attack.
		/// </summary>
		float Offence { get; }

		/// <summary>
		/// Percentage of user's piercing ability that gets transfered into the attack.
		/// </summary>
		float Piercing { get; }

		#endregion
	}
}
