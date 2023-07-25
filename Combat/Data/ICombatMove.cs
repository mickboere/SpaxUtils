using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for combat moves that can be charged and performed.
	/// </summary>
	public interface ICombatMove
	{
		/// <summary>
		/// Player facing name of this combat move.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Player facing description of this combat move.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// What should be done if this move is released before reaching <see cref="MinCharge"/>.
		/// TRUE: releasing input before reaching <see cref="MinCharge"/> will cancel the move.
		/// FALSE: releasing input before reaching <see cref="MinCharge"/> will continue until minimum and automatically perform.
		/// </summary>
		bool RequireMinCharge { get; }

		/// <summary>
		/// Minimum required charge in seconds before performing this move.
		/// </summary>
		float MinCharge { get; }

		/// <summary>
		/// Maximum charging extent in seconds.
		/// </summary>
		float MaxCharge { get; }

		/// <summary>
		/// The minimum performing duration of this move.
		/// Typically set to the performance-peak of the move's pose sequence.
		/// </summary>
		float MinDuration { get; }

		/// <summary>
		/// Interuptable release / fadeout time after the minimum duration.
		/// </summary>
		float Release { get; }

		/// <summary>
		/// The total performing duration of this move.
		/// Typically <see cref="MinDuration"/> + <see cref="Release"/>.
		/// </summary>
		float TotalDuration { get; }

		/// <summary>
		/// Collection of hit-box bone identifiers for this move's performance.
		/// </summary>
		List<string> HitBoxes { get; }

		/// <summary>
		/// The relative inertia of this combat move to apply to the user.
		/// </summary>
		public Vector3 Inertia { get; }

		/// <summary>
		/// The delay in applied momentum force into the performance.
		/// </summary>
		public float ForceDelay { get; }

		/// <summary>
		/// Stat identifier for the charge speed multiplier.
		/// </summary>
		string ChargeSpeedMultiplierStat { get; }

		/// <summary>
		/// Stat identifier for the perform speed multiplier.
		/// </summary>
		string PerformSpeedMultiplierStat { get; }

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

		/// <summary>
		/// The follow-up moves available during the release of this move.
		/// </summary>
		List<ActCombatPair> FollowUps { get; }

		/// <summary>
		/// Evaluates the combat move at <paramref name="chargeTime"/> seconds of charge and <paramref name="performTime"/> seconds of performance.
		/// </summary>
		/// <param name="chargeTime">The amount of seconds of charge.</param>
		/// <param name="performTime">The amount of seconds of performance.</param>
		/// <param name="weight">The resulting effective weight of the complete pose blend.</param>
		/// <returns><see cref="PoseTransition"/> resulting from the evaluation.</returns>
		PoseTransition Evaluate(float chargeTime, float performTime, out float weight);
	}
}
