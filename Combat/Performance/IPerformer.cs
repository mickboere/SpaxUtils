using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for classes that can perform an <see cref="IAct"/>.
	/// </summary>
	public interface IPerformer
	{
		event Action<IPerformer> PerformanceUpdateEvent;
		event Action<IPerformer> PerformanceCompletedEvent;

		/// <summary>
		/// Integer indicating the order of execution priority when dealing with multiple performers supporting the same acts.
		/// Higher priority takes precedense over lower priority.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// Returns which type of acts this performer is able to execute.
		/// </summary>
		List<string> SupportsActs { get; }

		/// <summary>
		/// Whether this performer is currently actively performing.
		/// </summary>
		bool Performing { get; }

		/// <summary>
		/// Whether this performer is currently finishing up its performance and ready for another one to begin.
		/// </summary>
		bool Finishing { get; }

		/// <summary>
		/// Whether this performer has completed its performance.
		/// Returns true when accessed in the last <see cref="PerformanceUpdateEvent"/>.
		/// </summary>
		bool Completed { get; }

		/// <summary>
		/// The amount of time the performance has been going for.
		/// </summary>
		float RunTime { get; }

		/// <summary>
		/// Try to prepare a performer to handle the <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act to try and produce for.</param>
		/// <returns>Whether the requested performance has succesfully been produced and ready for performance.</returns>
		bool TryProduce(IAct act, out IPerformer performer);

		/// <summary>
		/// Attempt to begin the performance currently being prepared.
		/// </summary>
		/// <returns>Whether the performance has been successfully begun.</returns>
		bool TryPerform();
	}
}
