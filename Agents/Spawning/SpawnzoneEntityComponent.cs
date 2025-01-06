using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public class SpawnzoneEntityComponent : EntityComponentMono
	{
		public WorldRegion Region => region;

		[SerializeField] private WorldRegion region;
		[SerializeField] private Transform[] points;
		[SerializeField] private AgentSetupAsset agentSetup;
		[SerializeField] AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			this.dependencyManager = dependencyManager;
		}

		protected void Start()
		{
			foreach (Transform point in points)
			{
				var spawnpoint = new Spawnpoint(null, point.position, point.rotation, region);

				DependencyManager agentDependencyManager = new DependencyManager(dependencyManager, agentSetup.Identification.Name);
				agentDependencyManager.Bind(spawnpoint);
				Agent agent = spawnData.Spawn(agentSetup, agentDependencyManager, spawnpoint.Position, spawnpoint.Rotation);
				agent.Brain.TryTransition(AgentStateIdentifiers.ACTIVE);
			}
		}

		protected virtual void OnDrawGizmos()
		{
			if (points == null)
			{
				return;
			}

			foreach (Transform point in points)
			{
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
