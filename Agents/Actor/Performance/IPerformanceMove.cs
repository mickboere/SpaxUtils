using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part performances (introduction/charge/preparation -> execution/performance -> outroduction/release).
	/// </summary>
	public interface IPerformanceMove
	{
		#region Metadata

		/// <summary>
		/// Player facing name of this combat move.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Player facing description of this combat move.
		/// </summary>
		string Description { get; }

		#endregion Metadata

		#region Animation

		/// <summary>
		/// The type of animation applied to the performer during performance.
		/// </summary>
		PerformanceAnimationType AnimationType { get; }

		/// <summary>
		/// The animation index corresponding to this move, to pass into the animator.
		/// </summary>
		int AnimationIndex { get; }

		/// <summary>
		/// Asset containing evaluatable pose data to be utilized by the lead-behaviour.
		/// </summary>
		PosingData PosingData { get; }

		#endregion Animation

		/// <summary>
		/// <see cref="BehaviourAsset"/>s to instantiate during performance.
		/// The behaviour assets are responsible for physically acting out all performance data.
		/// </summary>
		IReadOnlyList<BehaviourAsset> Behaviour { get; }

		/// <summary>
		/// The follow-up moves available while this move is active.
		/// </summary>
		IReadOnlyList<MoveFollowUp> FollowUps { get; }

		#region Preparation

		/// <summary>
		/// Whether the move can be charged or automatically performs.
		/// </summary>
		bool HasPrep { get; }

		/// <summary>
		/// Minimum required charge in seconds before performing this move.
		/// </summary>
		float MinPrep { get; }

		/// <summary>
		/// Maximum charging extent in seconds.
		/// </summary>
		float MaxPrep { get; }

		/// <summary>
		/// What should be done if this move is released before reaching <see cref="MinPrep"/>.
		/// TRUE: releasing input before reaching <see cref="MinPrep"/> will cancel the move.
		/// FALSE: releasing input before reaching <see cref="MinPrep"/> will continue until minimum and automatically perform.
		/// </summary>
		bool RequireMinPrep { get; }

		/// <summary>
		/// Stat identifier for the charge speed multiplier.
		/// </summary>
		string PrepSpeedMultiplierStat { get; }

		/// <summary>
		/// The stat-cost drained for charging this move.
		/// </summary>
		StatCost PrepCost { get; }

		#endregion Preparation

		#region Performance

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
		/// Duration of transition from charge pose to performing pose, relative to MinDuration.
		/// </summary>
		float ChargeFadeout { get; }

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
		/// The stat-cost drained for performing this move.
		/// </summary>
		StatCost PerformCost { get; }

		#endregion Performance

		/// <summary>
		/// Time it takes for the performance to fade away after canceling.
		/// </summary>
		float CancelDuration { get; }
	}
}
