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
		/// </summary>
		float MinDuration { get; }

		/// <summary>
		/// Interuptable release / fadeout time after the minimum duration.
		/// </summary>
		float Release { get; }

		/// <summary>
		/// The total performing duration of this move. Typically <see cref="MinDuration"/> + <see cref="Release"/>.
		/// </summary>
		float TotalDuration { get; }

		/// <summary>
		/// Impact applied to user upon performing.
		/// </summary>
		Impact Impact { get; }

		/// <summary>
		/// Stat identifier for the charge speed multiplier.
		/// </summary>
		string ChargeSpeedMultiplier { get; }

		/// <summary>
		/// Stat identifier for the perform speed multiplier.
		/// </summary>
		string PerformSpeedMultiplier { get; }

		/// <summary>
		/// Evaluates the combat move at <paramref name="chargeTime"/> seconds of charge and <paramref name="performTime"/> seconds of performance.
		/// </summary>
		/// <param name="fromPose">The pose to initiate the move from, used to transition into the charging pose.</param>
		/// <param name="chargeTime">The amount of seconds of charge.</param>
		/// <param name="performTime">The amount of seconds of performance.</param>
		/// <param name="weight">The effective weight of the complete pose blend.</param>
		/// <param name="additionalFromStanceData"></param>
		/// <returns></returns>
		PoseTransition Evaluate(float chargeTime, float performTime, out float weight);
	}
}
