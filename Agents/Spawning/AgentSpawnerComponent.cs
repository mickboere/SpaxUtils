using System.Collections;
using UnityEngine;

namespace SpaxUtils
{
	[RequireComponent(typeof(Entity))]
	public class AgentSpawnerComponent : EntityComponentBase
	{
		[SerializeField] private AgentSetupAsset agentSetup;
		[SerializeField] AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			this.dependencyManager = dependencyManager;
		}

		protected void Start()
		{
			ISpawnpoint spawnpoint = GetComponent<ISpawnpoint>();
			if (spawnpoint != null)
			{
				DependencyManager agentDependencyManager = new DependencyManager(dependencyManager, agentSetup.Identification.Name);
				agentDependencyManager.Bind(spawnpoint);
				spawnData.Spawn(agentSetup, agentDependencyManager, spawnpoint.Position, spawnpoint.Rotation);
			}
			else
			{
				SpaxDebug.Warning($"Spawner has no spawnpoint component attached.", $"Agent was not spawned.");
			}
		}
	}
}
