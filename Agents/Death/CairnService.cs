using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class CairnService : IService, IInitializable
	{
		private const string ID_CAIRN_COLLECTION = "CAIRNS";
		private const float DEGRADATION_PER_CYCLE = 1f / 8f;

		private RuntimeDataService runtimeDataService;
		private WorldService worldService;
		private SceneService sceneService;
		private EntityLibrary entityLibrary;
		private IDependencyManager dependencyManager;

		private List<string> cairnIDs = new List<string>();
		private Dictionary<string, Entity> cairnInstances = new Dictionary<string, Entity>();

		public CairnService(RuntimeDataService runtimeDataService, WorldService worldService, SceneService sceneService,
			EntityLibrary entityLibrary, IDependencyManager dependencyManager)
		{
			this.runtimeDataService = runtimeDataService;
			this.worldService = worldService;
			this.sceneService = sceneService;
			this.entityLibrary = entityLibrary;
			this.dependencyManager = dependencyManager;

			runtimeDataService.CurrentProfileChangedEvent += OnCurrentProfileChanged;
			worldService.WorldActiveChangedEvent += OnWorldActiveChanged;
			worldService.NewCycleEvent += OnNewCycle;
		}

		public void Initialize()
		{
			// Clear any previous data.
			ClearInstances();

			// Require a save profile to initialize.
			if (runtimeDataService.CurrentProfile == null)
			{
				return;
			}

			// Load cairn collection from profile, or initialize if not present.
			if (!runtimeDataService.CurrentProfile.TryGetValue(ID_CAIRN_COLLECTION, out cairnIDs))
			{
				cairnIDs = new List<string>();
			}
			else
			{
				SpawnInstances();
			}
		}

		private void OnCurrentProfileChanged(RuntimeDataCollection profile)
		{
			Initialize();
		}

		private void OnWorldActiveChanged(bool active)
		{
			// Activate / Deactivate all cairn instances.
			foreach (Entity cairn in cairnInstances.Values)
			{
				cairn.GameObject.SetActive(active);
			}
		}

		private void OnNewCycle(int cycle)
		{
			// 1. Clear out old cairn instances.
			ClearInstances();

			// 2. Spawn new cairns based on saved data.
			SpawnInstances();
		}

		/// <summary>
		/// Registers a new cairn to be spawned at the start of the next cycle, with the given data.
		/// The cairn will be assigned a new unique ID, which will be set in the given data collection, and used to identify the cairn in future cycles.
		/// </summary>
		public void RegisterCairn(IEntity entity, Vector3 position, RuntimeDataCollection data)
		{
			string id = System.Guid.NewGuid().ToString();
			cairnIDs.Add(id);
			data.ID = id;
			data.SetValue(EntityDataIdentifiers.POSITION, position);
			data.SetValue(EntityDataIdentifiers.SCENE, sceneService.CurrentScene);
			data.SetValue(EntityDataIdentifiers.CYCLE, worldService.Cycle + 1); // The cairn rises with the next cycle; its clock starts there.

			runtimeDataService.CurrentProfile.TryAdd(data);
			runtimeDataService.CurrentProfile.SetValue(ID_CAIRN_COLLECTION, cairnIDs);
		}

		/// <summary>
		/// Mark a cairn as having been retrieved or expired.
		/// </summary>
		public void DeleteCairn(string id)
		{
			if (cairnInstances.ContainsKey(id))
			{
				Object.Destroy(cairnInstances[id].GameObject);
				cairnInstances.Remove(id);
			}

			cairnIDs.Remove(id);
			runtimeDataService.CurrentProfile.TryRemove(id, true);
		}

		private void ClearInstances()
		{
			foreach (Entity cairn in cairnInstances.Values)
			{
				Object.Destroy(cairn);
			}
			cairnInstances.Clear();
		}

		private void SpawnInstances()
		{
			List<string> toDelete = new List<string>();
			foreach (string id in cairnIDs)
			{
				if (runtimeDataService.CurrentProfile.TryGetEntry(id, out RuntimeDataCollection cairnData))
				{
					// Degradation is deterministic from birth: scaling starts at the owner's alignment at death
					// and loses 1/8 per cycle. A saint's cairn rises at full health, a sinner's rises fully degraded.
					int elapsed = worldService.Cycle - cairnData.GetValue<int>(EntityDataIdentifiers.CYCLE);
					if (elapsed < 0)
					{
						// Not yet risen; the cairn stands from its birth cycle on.
						continue;
					}

					float alignment = cairnData.GetValue(EntityDataIdentifiers.ALIGNMENT, 1f);
					float scaling = alignment - elapsed * DEGRADATION_PER_CYCLE;

					if (scaling < -0.0001f)
					{
						// Spent: even the husk cycle has passed. Destroy, items included.
						toDelete.Add(id);
						continue;
					}

					// Clamp: the final cycle stands as an empty husk holding only items.
					cairnData.SetValue(EntityDataIdentifiers.SCALING, Mathf.Max(0f, scaling));

					// If cairn is not in the current scene, don't instantiate.
					if (sceneService.CurrentScene != cairnData.GetValue<string>(EntityDataIdentifiers.SCENE))
					{
						continue;
					}

					Vector3 position = cairnData.GetValue<Vector3>(EntityDataIdentifiers.POSITION);
					Entity cairn = entityLibrary.Instantiate(EntityIdentifiers.CAIRN_SOLEMN, id, position, Quaternion.identity, dependencyManager, cairnData,
						activate: worldService.WorldActive);

					cairnInstances.Add(id, cairn);
				}
				else
				{
					SpaxDebug.Error($"No cairn data found for ID '{id}'.");
				}
			}

			foreach (string cairn in toDelete)
			{
				DeleteCairn(cairn);
			}
		}
	}
}
