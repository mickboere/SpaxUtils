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
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private List<string> labels;
		[SerializeField] private ScriptableObject[] dependencies;

		[SerializeField, ConstDropdown(typeof(SpawnpointIdentifiers))] private string spawnpoint;

		[SerializeField] private LabeledDataCollection data;
		[SerializeField] private bool overwriteData;

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
				Agent agent = AgentFactory.Create(agentSetup, agentDependencyManager, spawnpoint.Position, spawnpoint.Rotation, labels: labels, dependencies: dependencies);
				data.ApplyToEntity(agent, overwriteData);
			}
			else
			{
				SpaxDebug.Error($"Was unable to find spawnpoint.", $"Tags ({string.Join(", ", this.spawnpoint)})");
			}
		}
	}
}
