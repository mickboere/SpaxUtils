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
		/// The amount of time this combat performance has spent charging.
		/// </summary>
		float Charge { get; }

		/// <summary>
		/// Whether this move has been canceled.
		/// </summary>
		bool Canceled { get; }

		/// <summary>
		/// The amount of time since this move has been canceled.
		/// </summary>
		float CancelTime { get; }
	}
}
