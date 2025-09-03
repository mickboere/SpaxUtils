using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Root <see cref="IMovePerformer"/> implementation that keeps track of all available <see cref="IPerformanceMove"/>s and handles initiating their performances.
	/// </summary>
	public interface IMovePerformanceHandler : IMovePerformer
	{
		/// <summary>
		/// Invoked each time the <see cref="Moveset"/> is updated.
		/// </summary>
		event Action MovesetUpdatedEvent;

		/// <summary>
		/// The current set of highest-priority moves per act.
		/// </summary>
		IReadOnlyDictionary<string, IPerformanceMove> Moveset { get; }

		/// <summary>
		/// Adds an <see cref="IPerformanceMove"/> to be performed upon <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act title for which the <paramref name="move"/> should be performed.</param>
		/// <param name="move">The move to be performed when the act is invoked.</param>
		/// <param name="state">The current state of performance required for this move to be performable.</param>
		/// <param name="prio">The priority of the move, used to order moves of the same act. Highest prio move gets executed.</param>
		/// <param name="prior">The prior move this move is a follow up to.</param>
		void AddMove(string act, IPerformanceMove move, PerformanceState state, int prio, IPerformanceMove prior = null);

		/// <summary>
		/// Removes a <see cref="IPerformanceMove"/> from the combat performer.
		/// </summary>
		/// <param name="act">The act to which the move is linked.</param>
		/// <param name="move">The move to remove from the performance handler.</param>
		void RemoveMove(string act, IPerformanceMove move);
	}
}
