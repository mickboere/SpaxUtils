using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that handles equipment data and visuals.
	/// </summary>
	public class EquipmentComponent : EntityComponentBase, IEquipmentComponent
	{
		public event Action<RuntimeEquipedData> EquipedEvent;
		public event Action<RuntimeEquipedData> UnequipedEvent;

		/// <inheritdoc/>
		public IReadOnlyCollection<IEquipmentSlot> Slots => slots.Values;

		/// <inheritdoc/>
		public IReadOnlyCollection<RuntimeEquipedData> EquipedItems => equipedItems.Values;

		private TransformLookup transformLookup;
		private SharedRigHandler sharedRigHandler;

		private ICommunicationChannel comms;

		private Dictionary<string, IEquipmentSlot> slots = new Dictionary<string, IEquipmentSlot>();
		private Dictionary<string, RuntimeEquipedData> equipedItems = new Dictionary<string, RuntimeEquipedData>();

		public void InjectDependencies(
			TransformLookup transformLookup,
			SharedRigHandler sharedRigHandler,
			ICommunicationChannel comms)
		{
			this.transformLookup = transformLookup;
			this.sharedRigHandler = sharedRigHandler;
			this.comms = comms;
		}

		protected void OnEnable()
		{
			comms.Listen<RequestOptionsMsg<RuntimeItemData>>(this, OnRequestInventoryItemOptionsMsg);
			comms.Listen<RequestOptionsMsg<RuntimeEquipedData>>(this, OnRequestEquipedItemOptionsMsg);
		}

		protected void OnDisable()
		{
			comms.StopListening(this);
		}

		/// <inheritdoc/>
		public bool TryGetSlot(string id, out IEquipmentSlot slot)
		{
			slot = null;
			if (slots.ContainsKey(id))
			{
				slot = slots[id];
			}

			return slot != null;
		}

		/// <inheritdoc/>
		public bool TryGetFreeSlot(string type, out IEquipmentSlot slot)
		{
			foreach (IEquipmentSlot s in Slots)
			{
				if (s.Type == type && !equipedItems.ContainsKey(s.UID))
				{
					slot = s;
					return true;
				}
			}

			slot = null;
			return false;
		}

		/// <inheritdoc/>
		public IEquipmentSlot AddNewSlot(string type, string id = null)
		{
			if (string.IsNullOrEmpty(id))
			{
				id = Guid.NewGuid().ToString();
			}
			else if (TryGetSlot(id, out IEquipmentSlot slot))
			{
				return slot;
			}

			slots.Add(id, new EquipmentSlot(id, type));
			return slots[id];
		}

		/// <inheritdoc/>
		public bool AddSlot(IEquipmentSlot slot)
		{
			if (string.IsNullOrEmpty(slot.UID))
			{
				SpaxDebug.Error($"Slot ID cannot be null or empty.");
				return false;
			}

			if (slots.ContainsKey(slot.UID))
			{
				SpaxDebug.Error($"Slot ID must be unique. '{slot.UID}' already exists.");
				return false;
			}

			slots.Add(slot.UID, slot);
			return true;
		}

		/// <inheritdoc/>
		public bool RemoveSlot(string id)
		{
			if (TryGetSlot(id, out IEquipmentSlot slot))
			{
				if (equipedItems.ContainsKey(id))
				{
					Unequip(equipedItems[id]);
				}

				slots.Remove(id);
				return true;
			}
			return false;
		}

		/// <inheritdoc/>
		public bool CanEquip(RuntimeItemData runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap)
		{
			// Make sure item data is equipment to begin with.
			if (runtimeItemData.ItemData is not IEquipmentData equipmentData)
			{
				slot = null;
				overlap = new List<RuntimeEquipedData>();
				return false;
			}

			// Conflicts occur when two items cover the same location(s), but they won't block equiping.
			overlap = EquipedItems.Where((e) => e.EquipmentData.CoversLocations.Any((c) => equipmentData.CoversLocations.Contains(c))).ToList();

			return TryGetFreeSlot(equipmentData.SlotType, out slot);
		}

		/// <inheritdoc/>
		public bool TryEquip(RuntimeItemData runtimeItemData, out RuntimeEquipedData equipedData)
		{
			// First make sure we can equip this item.
			if (CanEquip(runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> conflicts))
			{
				IEquipmentData itemData = runtimeItemData.ItemData as IEquipmentData;

				// Unequip conflicts.
				foreach (RuntimeEquipedData conflict in conflicts)
				{
					Unequip(conflict);
				}

				// Create a shared dependency manager for the equiped object and equipment data from the item's dependency manager.
				DependencyManager dependencyManager = new DependencyManager(runtimeItemData.DependencyManager, $"[EQUIPMENT]{runtimeItemData.Name}");

				// Instantiate item visual on the agent, deactivated to allow for dependency injection after the runtime data is created and bound.
				GameObject visual = InstantiateEquipmentDeactivated(itemData, slot);

				// Create and bind equiped data.
				equipedData = new RuntimeEquipedData(runtimeItemData, slot, dependencyManager, visual);
				dependencyManager.Bind(equipedData);

				if (visual != null)
				{
					// Bind and inject dependencies of the equiped object.
					DependencyUtils.Inject(visual, dependencyManager, true, true);
					visual.SetActive(true);
				}

				// Execute equipment behaviour.
				equipedData.ExecuteBehaviour();

				// Occupy the equipment spot and invoke equiped event.
				equipedItems[slot.UID] = equipedData;
				EquipedEvent?.Invoke(equipedData);

				return true;
			}

			SpaxDebug.Error("Couldn't equip item.", $"Slot=({slot.UID}); Type=({slot.Type}); Item=({runtimeItemData})");
			equipedData = null;
			return false;
		}

		/// <inheritdoc/>
		public void Unequip(RuntimeEquipedData equipedData)
		{
			if (equipedItems.ContainsKey(equipedData.Slot.UID))
			{
				// Stop all running equiped behaviour.
				equipedData.StopBehaviour();

				if (equipedData.EquipedVisual != null)
				{
					// Destroy the equiped object.
					sharedRigHandler.Remove(equipedData.EquipedVisual);
					Destroy(equipedData.EquipedVisual);
				}

				// Clear equiped slot.
				equipedItems.Remove(equipedData.Slot.UID);

				// Invoke unequiped event.
				UnequipedEvent?.Invoke(equipedData);

				// Dispose of all data.
				IDependencyManager dependencyManager = equipedData.DependencyManager;
				equipedData.Dispose();
				dependencyManager.Dispose();
			}
		}

		/// <inheritdoc/>
		public List<RuntimeEquipedData> GetEquiped(string slotType)
		{
			return EquipedItems.Where((e) => e.Slot.Type == slotType).ToList();
		}

		/// <summary>
		/// Instantiates the equipment data's prefab deactivated to allow for dependency injection afterwards. Returns null if there is no prefab to instantiate.
		/// </summary>
		private GameObject InstantiateEquipmentDeactivated(IEquipmentData equipmentData, IEquipmentSlot slot)
		{
			if (equipmentData.EquipedPrefab == null)
			{
				return null;
			}

			// Instantiate the equiped prefab in the correct parent.
			GameObject equipment = DependencyUtils.InstantiateDeactivated(equipmentData.EquipedPrefab);
			Transform parent = slot.Parent == null ? Entity.GameObject.transform : slot.Parent;
			equipment.transform.SetParent(parent);

			// Apply orientation.
			(Vector3 pos, Quaternion rot) orientation = slot.GetOrientation();
			equipment.transform.localPosition = orientation.pos;
			equipment.transform.localRotation = orientation.rot;

			// Apply the agent's rig when possible.
			sharedRigHandler.Share(equipment);

			return equipment;
		}

		private void OnRequestInventoryItemOptionsMsg(object msg)
		{
			var cast = (RequestOptionsMsg<RuntimeItemData>)msg;
			if (cast.Target.ItemData is IEquipmentData equipmentData)
			{
				Option equipOption = new Option(
					"Equip",
					$"Equips this item on an available '{equipmentData.SlotType}' slot.",
					(option) =>
					{
						TryEquip(cast.Target, out _);
					});

				cast.AddOption(equipOption);
			}
		}

		private void OnRequestEquipedItemOptionsMsg(object msg)
		{
			var cast = (RequestOptionsMsg<RuntimeEquipedData>)msg;

			Option equipOption = new Option(
				"Unequip",
				"Unequips this item.",
				(option) =>
				{
					Unequip(cast.Target);
				});

			cast.AddOption(equipOption);
		}
	}
}
