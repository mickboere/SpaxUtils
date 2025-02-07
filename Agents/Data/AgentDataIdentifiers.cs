namespace SpaxUtils
{
	public class AgentDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string AGENT = "AGENT/";
		public const string INVULNERABLE = AGENT + "Invulnerable";
		public const string RELATIONS = AGENT + "Relations";
		
		private const string AI = "AI/";
		public const string PARRY = AI + "Parry"; // Whether this AI should be able to parry attacks.
	}
}
