using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;
using System.Linq;

namespace SpaxUtils
{
	[NodeWidth(275)]
	public class SpawnAgentAtEntityNode : StateComponentNodeBase
	{
		[SerializeField] private AgentSetupAsset agentSetup;
		[SerializeField, ConstDropdown(typeof(ISpawnpointIdentifiers))] private string spawnpoint;
		[SerializeField] private AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;
		private IEntityCollection entityCollection;

		public void InjectDependencies(IDependencyManager dependencyManager, IEntityCollection entityCollection)
		{
			this.dependencyManager = dependencyManager;
			this.entityCollection = entityCollection;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			List<SpawnpointEntityComponent> foundSpawnpoints = entityCollection.GetComponents<SpawnpointEntityComponent>((e) => true, (s) => s.ID == this.spawnpoint);
			SpawnpointEntityComponent spawnpoint = foundSpawnpoints.FirstOrDefault();
			if (spawnpoint != null)
			{
				DependencyManager agentDependencyManager = new DependencyManager(dependencyManager, agentSetup.Identification.Name);
				agentDependencyManager.Bind(spawnpoint);
				spawnData.Spawn(agentSetup, agentDependencyManager, spawnpoint.Position, spawnpoint.Rotation);
			}
			else
			{
				SpaxDebug.Warning($"Was unable to find spawnpoint, agent was not spawned.", $"Spawnpoint tags: ({string.Join(", ", this.spawnpoint)})");
			}
		}
	}
}
