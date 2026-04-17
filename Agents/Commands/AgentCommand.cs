namespace SpaxUtils
{
	/// <summary>
	/// Commands that can be issued to agents through their communication channel.
	/// </summary>
	public enum AgentCommand
	{
		/// <summary>
		/// Navigate to and occupy a <see cref="PointOfInterest"/> by entity ID.
		/// </summary>
		OccupyPOI,

		/// <summary>
		/// Vacate the currently occupied <see cref="PointOfInterest"/>.
		/// </summary>
		VacatePOI,
	}
}
