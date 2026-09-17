namespace SpaxUtils
{
	/// <summary>
	/// How trail snapshots show the smear the body had when they were captured.
	/// </summary>
	public enum TrailSmearMode
	{
		/// <summary>No smear on the trail.</summary>
		None,
		/// <summary>Captured smear held still, streaks included.</summary>
		Frozen,
		/// <summary>Captured smear whose streaks keep scrolling, like the body's.</summary>
		Animated,
	}
}
