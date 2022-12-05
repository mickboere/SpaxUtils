namespace SpaxUtils
{
	public interface ICombatPerformer : IPerformer
	{
		/// <summary>
		/// The <see cref="ICombatMove"/> currently being performed.
		/// </summary>
		ICombatMove Current { get; }

		/// <summary>
		/// The state of the combat performance.
		/// </summary>
		CombatPerformanceState State { get; }

		/// <summary>
		/// The amount (of time) this combat performance has spent charging.
		/// </summary>
		float Charge { get; }

		#region State Getters

		/// <summary>
		/// Returns whether the performance is currently charging.
		/// </summary>
		bool Charging { get; }

		/// <summary>
		/// Returns whether the performance is currently attacking.
		/// </summary>
		bool Attacking { get; }

		/// <summary>
		/// Returns whether the performance is currently released.
		/// </summary>
		bool Released { get; }

		/// <summary>
		/// Returns whether the performance is currently finishing.
		/// </summary>
		bool Finishing { get; }

		#endregion

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
