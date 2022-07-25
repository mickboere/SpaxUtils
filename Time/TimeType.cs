namespace SpaxUtils
{
	/// <summary>
	/// The different types of time available.
	/// </summary>
	public enum TimeType
	{
		/// <summary>
		/// Real time passed since creating the loaded profile - persistent through sessions.
		/// Calculated from system time instead of being tracked like the playtime.
		/// </summary>
		Realtime,

		/// <summary>
		/// Amount of unscaled time spent in the game on the loaded profile.
		/// </summary>
		UnscaledPlaytime,

		/// <summary>
		/// Amount of scaled time spent in the game on the loaded profile.
		/// </summary>
		ScaledPlaytime,
	}
}