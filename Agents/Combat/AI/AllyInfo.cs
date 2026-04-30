namespace SpaxUtils
{
	/// <summary>
	/// Data container for the awareness of a single ally.
	/// </summary>
	public class AllyInfo : AgentTrackingInfo
	{
		/// <summary>True when this ally matches the FOLLOWING runtime data entry.</summary>
		public bool IsFollowTarget { get; set; }

		public AllyInfo(IAgent agent) : base(agent) { }
	}
}
