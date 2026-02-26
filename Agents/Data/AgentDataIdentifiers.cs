namespace SpaxUtils
{
	public class AgentDataIdentifiers : ILabeledDataIdentifiers
	{
		// AGENT DATA
		private const string AGENT = "AGENT/";
		public const string INVULNERABLE = AGENT + "Invulnerable"; // Bool
		public const string RELATIONS = AGENT + "Relations"; // Collection
		public const string ELEVATION = AGENT + "Elevation"; // Float
		public const string FEET_SURFACE = AGENT + "FeetSurface"; // String

		// AI DATA
		private const string AI = "AI/";
		public const string PARRY = AI + "Parry"; // Whether this AI should be able to parry attacks.
	}
}
