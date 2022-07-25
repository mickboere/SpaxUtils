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

		#region State getters
		bool Charging { get; }
		bool Attacking { get; }
		bool Released { get; }
		bool Finishing { get; }
		bool Completed { get; }
		#endregion
	}
}
