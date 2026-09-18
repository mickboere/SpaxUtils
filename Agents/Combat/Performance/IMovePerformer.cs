using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IPerformer"/> implementation used for performing <see cref="IPerformanceMove"/>s.
	/// </summary>
	public interface IMovePerformer : IPerformer
	{
		/// <summary>
		/// The <see cref="IPerformanceMove"/> currently being performed.
		/// </summary>
		IPerformanceMove Move { get; }

		/// <summary>
		/// The amount of time this combat performance has spent preparing the performance (charging).
		/// </summary>
		float ChargeTime { get; }

		/// <summary>
		/// The current charge multiplier of the performance; 1 = uncharged, rising while charging.
		/// Surfaced from the active <see cref="IChargeProvider"/> behaviour; 1 when none is charging.
		/// </summary>
		float ChargeMultiplier { get; }

		/// <summary>How charged the performance is, 0..1 of what the Static pool could fund; 0 when none.</summary>
		float ChargeFraction { get; }

		/// <summary>Whether the charge drained the pool dry and is waiting out its auto-release.</summary>
		bool ChargeDepleted { get; }

		/// <summary>
		/// Whether the current performance should halt its the runtime once minimum duration has been reached.
		/// </summary>
		bool Prolong { get; set; }

		/// <summary>
		/// Completes the charge and performs NOW, RunTime seconds in, whatever the clock says. For a
		/// reaction that must land on the frame it was earned, not once a hit pause lets time run again.
		/// </summary>
		bool PerformNow(float runTime);
	}
}
