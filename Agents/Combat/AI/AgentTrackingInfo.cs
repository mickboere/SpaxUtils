using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Shared spatial and component data for a tracked agent (enemy or ally).
	/// </summary>
	public class AgentTrackingInfo
	{
		public IAgent Agent { get; }
		public AgentStatHandler StatHandler { get; }
		public AgentCombatComponent CombatComp { get; }

		public bool Visible { get; set; }
		public float LastSeen { get; set; }
		public Vector3 LastLocation { get; set; }
		public float Distance { get; set; }
		public Vector3 Direction { get; set; }

		public AgentTrackingInfo(IAgent agent)
		{
			Agent = agent;
			StatHandler = agent.GetEntityComponent<AgentStatHandler>();
			CombatComp = agent.GetEntityComponent<AgentCombatComponent>();
		}
	}
}
