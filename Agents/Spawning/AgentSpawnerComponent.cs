using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Single slot <see cref="AgentSpawnerBase"/> implementation.
	/// </summary>
	[RequireComponent(typeof(Entity))]
	public class AgentSpawnerComponent : AgentSpawnerBase
	{
		protected override int GetSlotCount()
		{
			return 1;
		}

		protected override bool TryGetSpawnpoint(int slotIndex, out ISpawnpoint spawnpoint)
		{
			spawnpoint = GetComponent<ISpawnpoint>();
			return spawnpoint != null;
		}

		protected void OnDrawGizmos()
		{
			ISpawnpoint spawnpoint = GetComponent<ISpawnpoint>();
			if (spawnpoint == null)
			{
				return;
			}

			if (agentSetup != null && agentSetup.Body != null)
			{
				Gizmos.matrix = transform.localToWorldMatrix;
				Gizmos.color = Color.blue;
				Gizmos.DrawWireCube(
					Vector3.up * agentSetup.Body.BaseSize.y * 0.5f * agentSetup.Body.Scale,
					agentSetup.Body.BaseSize * agentSetup.Body.Scale);
			}
		}
	}
}
