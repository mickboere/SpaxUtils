using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public class AgentSpawnzoneComponent : AgentSpawnerBase
	{
		public WorldRegion Region => region;

		[Header("Zone")]
		[SerializeField] private WorldRegion region;
		[SerializeField] private Transform[] points;

		protected override int GetSlotCount()
		{
			return points == null ? 0 : points.Length;
		}

		protected override bool TryGetSpawnpoint(int slotIndex, out ISpawnpoint spawnpoint)
		{
			spawnpoint = null;

			if (points == null || slotIndex < 0 || slotIndex >= points.Length)
			{
				return false;
			}

			Transform point = points[slotIndex];
			if (point == null)
			{
				return false;
			}

			spawnpoint = new Spawnpoint(null, point.position, point.rotation, region);
			return true;
		}

		protected virtual void OnDrawGizmos()
		{
			if (points == null)
			{
				return;
			}

			foreach (Transform point in points)
			{
				if (point == null)
				{
					continue;
				}

				Gizmos.matrix = point.localToWorldMatrix;
				Gizmos.color = Color.red;
				Gizmos.DrawSphere(Vector3.zero, 0.1f);
				Gizmos.color = Color.blue;
				Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.5f);
				Gizmos.color = Color.magenta;
				Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
				Gizmos.color = Color.blue;
				Gizmos.DrawCube(Vector3.forward * 0.5f, Vector3.one * 0.1f);
			}
		}
	}
}
