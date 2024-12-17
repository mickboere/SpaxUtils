using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace SpaxUtils
{
	/// <summary>
	/// Spawns the player entities and cleans them up once this node is exits.
	/// </summary>
	[NodeWidth(250)]
	public class MaintainPlayerEntities : StateComponentNodeBase
	{
		[SerializeField] private PlayerConfig playerConfig;
		[SerializeField, ConstDropdown(typeof(ISpawnpointIdentifiers))] private string defaultSpawnpoint;
		[SerializeField] private AgentSpawnData spawnData;

		private IDependencyManager dependencyManager;
		private IEntityCollection entityCollection;
		private PlayerAgentService playerAgentService;

		private List<GameObject> instances = new List<GameObject>();

		public void InjectDependencies(IDependencyManager dependencyManager, IEntityCollection entityCollection,
			PlayerAgentService playerAgentService)
		{
			this.dependencyManager = dependencyManager;
			this.entityCollection = entityCollection;
			this.playerAgentService = playerAgentService;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);

			List<IEntity> foundSpawnpoints = entityCollection.Get<IEntity>((entity) => entity.ID == defaultSpawnpoint);
			Transform spawnpoint = foundSpawnpoints.FirstOrDefault()?.GameObject.transform;
			if (spawnpoint != null)
			{
				bool wasAlive = playerAgentService.IsAlive();
				IAgent player = playerAgentService.SpawnPlayer(dependencyManager, playerConfig, spawnData, spawnpoint, out instances);
				player.Brain.TryTransition(AgentStateIdentifiers.ACTIVE);
			}
			else
			{
				SpaxDebug.Error("Was unable to find spawnpoint.", this.defaultSpawnpoint);
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			foreach (GameObject instance in instances)
			{
				Destroy(instance);
			}
			instances.Clear();
		}
	}
}
