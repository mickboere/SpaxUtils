using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part performances (introduction/charge/preparation -> execution/performance -> outroduction/release).
	/// </summary>
	public interface IPerformanceMove
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
		/// <see cref="BehaviourAsset"/>s to instantiate during performance.
		/// </summary>
		IReadOnlyList<BehaviourAsset> Behaviour { get; }

		/// <summary>
		/// The follow-up moves available while this move is active.
		/// </summary>
		IReadOnlyList<MoveFollowUp> FollowUps { get; }

		/// <summary>
		/// Whether the move can be charged or automatically performs.
		/// </summary>
		bool HasCharge { get; }

		/// <summary>
		/// Minimum required charge in seconds before performing this move.
		/// </summary>
		float MinCharge { get; }

		/// <summary>
		/// Maximum charging extent in seconds.
		/// </summary>
		float MaxCharge { get; }

		/// <summary>
		/// What should be done if this move is released before reaching <see cref="MinCharge"/>.
		/// TRUE: releasing input before reaching <see cref="MinCharge"/> will cancel the move.
		/// FALSE: releasing input before reaching <see cref="MinCharge"/> will continue until minimum and automatically perform.
		/// </summary>
		bool RequireMinCharge { get; }

		/// <summary>
		/// Stat identifier for the charge speed multiplier.
		/// </summary>
		string ChargeSpeedMultiplierStat { get; }

		/// <summary>
		/// What should be done if this move is released after preparing.
		/// TRUE: Performance will begin automatically after input is released and preparation is ready.
		/// FALSE: Performance will cancel after input is released and preparation is ready.
		/// </summary>
		bool HasPerformance { get; }

		/// <summary>
		/// The minimum performing duration of this move.
		/// </summary>
		float MinDuration { get; }

		/// <summary>
		/// Pose fadeout time after RunTime exeeds the minimum duration.
		/// </summary>
		float Release { get; }

		/// <summary>
		/// The total performing duration of this move.
		/// Typically <see cref="MinDuration"/> + <see cref="Release"/>.
		/// </summary>
		float TotalDuration { get; }

		/// <summary>
		/// Stat identifier for the perform speed multiplier.
		/// </summary>
		string PerformSpeedMultiplierStat { get; }

		/// <summary>
		/// Time it takes for the performance to fade away after canceling.
		/// </summary>
		float CancelDuration { get; }

		/// <summary>
		/// Evaluates the combat move at <paramref name="chargeTime"/> seconds of charge and <paramref name="performTime"/> seconds of performance.
		/// </summary>
		/// <param name="chargeTime">The amount of seconds of charge.</param>
		/// <param name="performTime">The amount of seconds of performance.</param>
		/// <param name="weight">The resulting effective weight of the complete pose blend.</param>
		/// <param name="cancelTime">If the move has been canceled, how long has it been canceled for?</param>
		/// <returns><see cref="PoseTransition"/> resulting from the evaluation.</returns>
		PoseTransition Evaluate(float chargeTime, float performTime, out float weight, float cancelTime = 0f);
	}
}
