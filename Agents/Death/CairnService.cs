using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class CairnService : IService, IInitializable
	{
		private const string ID_CAIRN_COLLECTION = "CAIRNS";
		private const float DEGRADATION_THRESHOLD = 0.5f;

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
				SpawnInstances(false);
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
			SpawnInstances(true);
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
			data.SetValue(EntityDataIdentifiers.CYCLE, worldService.Cycle);

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

		private void SpawnInstances(bool newCycle)
		{
			Dictionary<string, SpiritAlignment> ownerAlignment = new Dictionary<string, SpiritAlignment>();
			List<string> toDelete = new List<string>();
			foreach (string id in cairnIDs)
			{
				if (runtimeDataService.CurrentProfile.TryGetEntry(id, out RuntimeDataCollection cairnData))
				{
					// Retrieve owner alignment.
					string owner = cairnData.GetValue<string>(EntityDataIdentifiers.ID);
					if (!ownerAlignment.TryGetValue(owner, out SpiritAlignment alignment))
					{
						RuntimeDataCollection ownerData = runtimeDataService.CurrentProfile.GetEntry<RuntimeDataCollection>(owner);
						float sin = ownerData.GetValue<float>(AgentStatIdentifiers.SIN, 100f);
						float virtue = ownerData.GetValue<float>(AgentStatIdentifiers.VIRTUE, 100f);
						alignment = SpaxFormulas.GetSpiritAlignment(sin, virtue);
					}

					// If entering new cycle, degrade cairn depending on owner alignment.
					if (newCycle && DegradeCairn(cairnData, alignment))
					{
						// Cairn is degraded, destroy it.
						toDelete.Add(id);
						continue;
					}

					// If cairn is not in the current scene, don't instantiate.
					if (sceneService.CurrentScene != cairnData.GetValue<string>(EntityDataIdentifiers.SCENE))
					{
						continue;
					}

					// Cairn type is decided per cycle.
					// This way, a sinner cannot retrieve a long forgotten sacred cairn, soon as he turns sinner, the cairn turns sullied.
					string prefabType = alignment == SpiritAlignment.Sinner ? EntityIdentifiers.CAIRN_NEGATIVE :
						alignment == SpiritAlignment.Neutral ? EntityIdentifiers.CAIRN_NEUTRAL : EntityIdentifiers.CAIRN_POSITIVE;

					Vector3 position = cairnData.GetValue<Vector3>(EntityDataIdentifiers.POSITION);
					Entity cairn = entityLibrary.Instantiate(prefabType, id, position, Quaternion.identity, dependencyManager, cairnData,
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

		private bool DegradeCairn(RuntimeDataCollection cairnData, SpiritAlignment alignment)
		{
			float scaling = cairnData.GetValue(EntityDataIdentifiers.SCALING, 1f);
			switch (alignment)
			{
				case SpiritAlignment.Sinner:
					scaling *= 0.512f; // EXP degrades after 1 cycle.
					break;

				case SpiritAlignment.Neutral:
					scaling *= 0.8f; // EXP degrades after 3 cycles.
					break;

				case SpiritAlignment.Saint:
					// Cairns of the sacred do not degrade, they are even restored.
					scaling = 1f;
					break;
			}

			if (scaling < DEGRADATION_THRESHOLD)
			{
				scaling = 0f;
			}

			cairnData.SetValue(EntityDataIdentifiers.SCALING, scaling);

			// If EXP is degraded and the cairn contains no items, the cairn is degraded and destroyed.
			// Optionally we can make it so that items also get lost for sinners / neutrals.
			bool hasItems = false;
			if (cairnData.TryGetEntry(InventoryComponent.INVENTORY_DATA_ID, out RuntimeDataCollection inventoryData))
			{
				hasItems = inventoryData.Data.Count > 0;
			}

			return scaling < DEGRADATION_THRESHOLD && !hasItems;
		}
	}
}
