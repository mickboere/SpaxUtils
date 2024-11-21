using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	public class AgentStateIdentifiers : IStateIdentifiers
	{
		public const string INACTIVE = "Inactive";
		public const string ACTIVE = "Active";
		public const string CONTROL = "Control";
		public const string PASSIVE = "Passive";
		public const string COMBAT = "Combat";
		public const string DEAD = "Dead";
		public const string CUTSCENE = "Cutscene";
	}
}
