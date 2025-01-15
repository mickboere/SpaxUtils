namespace SpaxUtils
{
	public class AgentDataIdentifiers : ILabeledDataIdentifiers
	{
		public const string RELATIONS = "Relations";

		private const string AGENT = "AGENT/";
		public const string PERSONALITY = AGENT + "Personality";

		private const string AI = "AI/";
		public const string PARRY = AI + "Parry"; // Whether this AI should be able to parry attacks.
	}
}
