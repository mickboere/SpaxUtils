namespace SpaxUtils
{
	/// <summary>
	/// Framework <see cref="TimelineMarker"/> identifiers. Games can add their own class implementing
	/// <see cref="ITimelineMarkerIdentifiers"/> and its constants will appear in the same dropdowns.
	/// </summary>
	public class TimelineMarkerIdentifiers : ITimelineMarkerIdentifiers
	{
		// The three phase regions chain end-to-end and mirror PerformanceState exactly:
		// Charging ends where Performing starts, Performing ends where Finishing starts.
		public const string CHARGING = ITimelineMarkerIdentifiers.PERFORMANCE + "Charging";

		/// <summary>Optional, between Charging and Performing. Played across a lunge or storm approach.</summary>
		public const string LUNGING = ITimelineMarkerIdentifiers.PERFORMANCE + "Lunging";

		public const string PERFORMING = ITimelineMarkerIdentifiers.PERFORMANCE + "Performing";
		public const string FINISHING = ITimelineMarkerIdentifiers.PERFORMANCE + "Finishing";

		public const string HIT = ITimelineMarkerIdentifiers.PERFORMANCE + "Hit";
		public const string INERTIA = ITimelineMarkerIdentifiers.PERFORMANCE + "Inertia";
	}
}
