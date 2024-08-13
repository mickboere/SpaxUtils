namespace SpaxUtils
{
	/// <summary>
	/// Base interface for all <see cref="IPerformanceMove"/>s used in combat.
	/// </summary>
	public interface ICombatMove : IPerformanceMove
	{
		/// <summary>
		/// The (base) range of this combat move.
		/// </summary>
		float Range { get; }
	}
}
