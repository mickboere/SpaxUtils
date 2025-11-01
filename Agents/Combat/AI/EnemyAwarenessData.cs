using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Data container for the awareness of a single enemy.
	/// </summary>
	public class EnemyAwarenessData
	{
		/// <summary>
		/// The enemy agent.
		/// </summary>
		public IAgent Agent { get; set; }

		#region Spatial

		/// <summary>
		/// When was the enemy last spotted (game-time).
		/// </summary>
		public float LastSeen { get; set; }

		/// <summary>
		/// Where was the enemy last spotted (world-pos).
		/// </summary>
		public Vector3 LastLocation { get; set; }

		/// <summary>
		/// The normalized direction from the Agent towards the Enemy.
		/// </summary>
		public Vector3 Direction { get; set; }

		/// <summary>
		/// Current distance from the Agent to the Enemy.
		/// </summary>
		public float Distance { get; set; }

		/// <summary>
		/// The projected decrease in distance over 1 second.
		/// Projection is negative when Agent and Enemy are moving away from each other.
		/// Projection is positive when Agent and Enemy are closing in on each other.
		/// </summary>
		public float Projection { get; set; }

		/// <summary>
		/// The base attacking range of the enemy when standing still.
		/// </summary>
		public float Reach { get; set; }

		#endregion Spatial

		#region Combat

		/// <summary>
		/// How negative the relationship score is between the Agent and Enemy.
		/// </summary>
		public float Resentment { get; set; }

		/// <summary>
		/// How much of a threat the Enemy currently is to the Agent.
		/// (should be) Based on distance relative to each other's reach.
		/// </summary>
		public float Threat { get; set; }

		/// <summary>
		/// How open the Enemy currently is to being attacked.
		/// </summary>
		public float Oppurtunity { get; set; }

		/// <summary>
		/// The sum of the Agent's current pointstats divided by the Enemy's current pointstats.
		/// </summary>
		public float Advantage { get; set; }

		/// <summary>
		/// The sum of the Enemy's current pointstats divided by the Agent's current pointstats.
		/// </summary>
		public float Disadvantage { get; set; }

		#endregion Combat

		public EnemyAwarenessData(IAgent agent)
		{
			Agent = agent;
		}
	}
}
