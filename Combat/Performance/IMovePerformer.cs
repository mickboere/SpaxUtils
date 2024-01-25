using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IPerformer"/> implementation used for performing <see cref="IPerformanceMove"/>s.
	/// </summary>
	public interface IMovePerformer : IPerformer
	{
		/// <summary>
		/// Invoked whenever the desired agent-pose has been updated.
		/// </summary>
		event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		/// <summary>
		/// Event invoked when the combat performer has encountered new hits during an attack.
		/// </summary>
		//event Action<List<HitScanHitData>> NewHitDetectedEvent;

		/// <summary>
		/// Event invoked while hitting a <see cref="IHittable"/>, before the <see cref="HitData"/> is sent to the hit object.
		/// </summary>
		//event Action<HitData> ProcessHitEvent;

		/// <summary>
		/// The <see cref="IPerformanceMove"/> currently being performed.
		/// </summary>
		IPerformanceMove Move { get; }

		/// <summary>
		/// The amount of time this combat performance has spent charging.
		/// </summary>
		float Charge { get; }

		/// <summary>
		/// Adds an <see cref="IPerformanceMove"/> to be performed upon <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act title for which the <paramref name="move"/> should be performed.</param>
		/// <param name="owner">The owner of the move added to the moveset. Used for easily identifying and removing moves.</param>
		/// <param name="state">The current state of performance required for this move to be performable.</param>
		/// <param name="move">The move to be performed when the act is invoked.</param>
		/// <param name="prio">The priority of the move, used to order moves of the same act. Highest prio move gets executed.</param>
		void AddMove(string act, object owner, PerformanceState state, IPerformanceMove move, int prio);

		/// <summary>
		/// Removes a <see cref="IPerformanceMove"/> from the combat performer.
		/// </summary>
		/// <param name="act">The act to which the move is linked.</param>
		/// <param name="move">The move to remove from the combat performer.</param>
		void RemoveMove(string act, object owner);
	}
}
