using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IPerformer"/> implementation used for performing combat upon acting.
	/// </summary>
	public interface ICombatPerformer : IPerformer
	{
		/// <summary>
		/// Invoked whenever the desired agent-pose has been updated.
		/// </summary>
		event Action<IPerformer, PoserStruct, float> PoseUpdateEvent;

		/// <summary>
		/// Event invoked when the combat performer has encountered new hits during an attack.
		/// </summary>
		event Action<List<HitScanHitData>> NewHitDetectedEvent;

		/// <summary>
		/// Event invoked while hitting a <see cref="IHittable"/>, before the <see cref="HitData"/> is sent to the hit object.
		/// </summary>
		event Action<HitData> ProcessHitEvent;

		/// <summary>
		/// The <see cref="ICombatMove"/> currently being performed.
		/// </summary>
		ICombatMove CurrentMove { get; }

		/// <summary>
		/// The amount (of time) this combat performance has spent charging.
		/// </summary>
		float Charge { get; }

		/// <summary>
		/// Adds an <see cref="ICombatMove"/> to be performed upon <paramref name="act"/>.
		/// </summary>
		/// <param name="act">The act title for which the <paramref name="move"/> should be performed.</param>
		/// <param name="move">The move to be performed when the act is invoked.</param>
		/// <param name="prio">The priority of the move, used to order moves of the same act. Highest prio move gets executed.</param>
		void AddCombatMove(string act, ICombatMove move, int prio);

		/// <summary>
		/// Removes a <see cref="ICombatMove"/> from the combat performer.
		/// </summary>
		/// <param name="act">The act to which the move is linked.</param>
		/// <param name="move">The move to remove from the combat performer.</param>
		void RemoveCombatMove(string act, ICombatMove move);
	}
}
