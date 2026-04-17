using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Data container for the awareness of a single enemy.
	/// </summary>
	public class EnemyInfo
	{
		/// <summary>The enemy agent.</summary>
		public IAgent Agent { get; set; }

		/// <summary>The enemy agents' stat handler, which exposes all point-stats.</summary>
		public AgentStatHandler StatHandler { get; set; }

		/// <summary>The enemy agent's combat component which exposes live combat info.</summary>
		public AgentCombatComponent CombatComp { get; set; }

		#region Spatial

		/// <summary>Whether the enemy is currently visible to the agent.</summary>
		public bool Visible { get; set; }

		/// <summary>When was the enemy last spotted (game-time).</summary>
		public float LastSeen { get; set; }

		/// <summary>Where was the enemy last spotted (world-pos).</summary>
		public Vector3 LastLocation { get; set; }

		/// <summary>The normalized direction from the Agent towards the Enemy.</summary>
		public Vector3 Direction { get; set; }

		/// <summary>Current distance from the Agent to the Enemy.</summary>
		public float Distance { get; set; }

		/// <summary>
		/// Relative closing speed along the line Agent→Enemy (m/s).
		/// Positive when closing, negative when moving apart.
		/// </summary>
		public float ClosingSpeed { get; set; }

		/// <summary>
		/// Time (seconds) until the enemy can hit the agent if both keep their current velocities.
		/// Infinity (float.PositiveInfinity) when not closing.
		/// </summary>
		public float TimeToHit { get; set; }

		#endregion Spatial

		#region Combat

		/// <summary>
		/// How negative the relationship score is between the Agent and Enemy.
		/// (0 = neutral, 1 = hates, -1 = loves)
		/// </summary>
		public float Resentment { get; set; }

		/// <summary>
		/// How dangerous the enemy is purely from stat difference (0-1).
		/// ~0 = much weaker, ~0.5 = equal, ~1 = much stronger.
		/// </summary>
		public float Lethality { get; set; }

		/// <summary>How “ready and focused” the enemy is to attack this agent (0-1).</summary>
		public float Intent { get; set; }

		/// <summary>How threatening the enemy currently is to the Agent (0-1).</summary>
		public float Threat { get; set; }

		/// <summary>How open the Enemy currently is to being attacked.</summary>
		public float Oppurtunity { get; set; }

		#endregion Combat

		public EnemyInfo(IAgent agent)
		{
			Agent = agent;
			StatHandler = agent.GetEntityComponent<AgentStatHandler>();
			CombatComp = agent.GetEntityComponent<AgentCombatComponent>();
		}
	}
}
