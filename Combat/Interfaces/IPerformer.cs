using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IActor"/>-dependant components that can perform for an <see cref="IAct"/>.
	/// </summary>
	public interface IPerformer
	{
		public delegate void PerformanceUpdateDelegate(IPerformer performer, PoserStruct pose, float weight);
		event PerformanceUpdateDelegate PerformanceUpdateEvent;
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
		/// When true will block all other incoming acts from performing until false.
		/// </summary>
		bool Performing { get; }

		/// <summary>
		/// Returns true when accessed in the last <see cref="PerformanceUpdateEvent"/>.
		/// </summary>
		bool Completed { get; }

		/// <summary>
		/// The amount of time the performance has been going for.
		/// </summary>
		float PerformanceTime { get; }

		/// <summary>
		/// Try to prepare a performer to handle the <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act to try and produce for.</param>
		/// <returns>Whether the requested performance has succesfully been produced and ready for performance.</returns>
		bool TryProduce(IAct act, out IPerformer performer);

		/// <summary>
		/// Attempt to release the performance currently being prepared.
		/// </summary>
		/// <returns>Whether the performance has been successfully released.</returns>
		bool TryPerform();
	}
}
