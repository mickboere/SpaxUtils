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
		/// The follow-up moves available during the release of this move.
		/// </summary>
		List<ActCombatPair> FollowUps { get; }

		/// <summary>
		/// The relative inertia of this combat move to apply to the user.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The delay in applied momentum force into the performance.
		/// </summary>
		public float ForceDelay { get; }

		/// <summary>
		/// Stat identifier for the amount of force applied upon a succesful hit.
		/// </summary>
		string StrengthStat { get; }

		/// <summary>
		/// Whether this move is offensive and can damage other entities.
		/// </summary>
		bool Offensive { get; }

		/// <summary>
		/// Stat identifier for the damage calculation.
		/// </summary>
		string OffenceStat { get; }

		/// <summary>
		/// Multiplier value for the offence stat.
		/// </summary>
		float Offensiveness { get; }

		/// <summary>
		/// The cost to entity stat(s) for performing this move.
		/// </summary>
		IList<StatCost> PerformCost { get; }
	}
}
