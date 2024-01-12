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

		#region Properties

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
		/// The state of the current performance.
		/// </summary>
		Performance State { get; }

		/// <summary>
		/// The amount of time the performance has been going for.
		/// </summary>
		float RunTime { get; }

		#endregion Properties

		#region Methods

		/// <summary>
		/// Try to prepare a performer to handle the <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act to try and prepare for.</param>
		/// <returns>Whether the requested performance has succesfully been prepared and is ready for performance.</returns>
		bool TryPrepare(IAct act, out IPerformer performer);

		/// <summary>
		/// Attempt to begin the performance currently being prepared.
		/// </summary>
		/// <returns>Whether the performance has been successfully begun.</returns>
		bool TryPerform();

		/// <summary>
		/// Will try the cancel the current performance.
		/// </summary>
		/// <returns>Whether the performance has been succesfully canceled.</returns>
		bool TryCancel();

		#endregion
	}
}
