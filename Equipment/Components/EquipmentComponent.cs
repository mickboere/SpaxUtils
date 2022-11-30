using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that handles equipment data and visuals.
	/// </summary>
	[DefaultExecutionOrder(100)]
	public class EquipmentComponent : InteractorBase, IEquipmentComponent
	{
		public const string EQUIPMENT_DATA_ID = "Equipment";

		public event Action<RuntimeEquipedData> EquipedEvent;
		public event Action<RuntimeEquipedData> UnequipingEvent;

		/// <inheritdoc/>
		public IReadOnlyCollection<IEquipmentSlot> Slots => slots.Values;

		/// <inheritdoc/>
		public IReadOnlyCollection<RuntimeEquipedData> EquipedItems => equipedItems.Values;

		private SharedRigHandler sharedRigHandler;
		private ICommunicationChannel comms;
		private InventoryComponent inventoryComponent;

		private RuntimeDataCollection equipmentData;
		private Dictionary<string, IEquipmentSlot> slots = new Dictionary<string, IEquipmentSlot>();
		private Dictionary<string, RuntimeEquipedData> equipedItems = new Dictionary<string, RuntimeEquipedData>(); // string = slot.UID

		public void InjectDependencies(SharedRigHandler sharedRigHandler,
			ICommunicationChannel comms, InventoryComponent inventoryComponent)
		{
			this.sharedRigHandler = sharedRigHandler;
			this.comms = comms;
			this.inventoryComponent = inventoryComponent;

			equipmentData = Entity.RuntimeData.GetEntry(EQUIPMENT_DATA_ID, new RuntimeDataCollection(EQUIPMENT_DATA_ID));
		}

		protected void Start()
		{
			// Load equipment
			foreach (RuntimeDataEntry entry in equipmentData.Data)
			{
				TryEquip(inventoryComponent.Inventory.Get((string)entry.Value), out _, entry.ID);
			}
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

		#region IEquipmentComponent

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
				if (s.Type == type && !equipedItems.ContainsKey(s.ID))
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
			if (string.IsNullOrEmpty(slot.ID))
			{
				SpaxDebug.Error($"Slot ID cannot be null or empty.");
				return false;
			}

			if (slots.ContainsKey(slot.ID))
			{
				SpaxDebug.Error($"Slot ID must be unique. '{slot.ID}' already exists.");
				return false;
			}

			slots.Add(slot.ID, slot);
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
		public bool CanEquip(RuntimeItemData runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap, string slotId = null)
		{
			slot = null;

			// Make sure item data is equipment to begin with.
			if (runtimeItemData.ItemData is not IEquipmentData equipmentData)
			{
				overlap = new List<RuntimeEquipedData>();
				return false;
			}

			// Conflicts occur when two items cover the same location(s), but they won't block equiping.
			overlap = EquipedItems.Where((e) => e.EquipmentData.CoversLocations.Any((c) => equipmentData.CoversLocations.Contains(c))).ToList();

			// Check if supplied slotID is available.
			if (slotId != null)
			{
				if (slots.ContainsKey(slotId) && !equipedItems.ContainsKey(slotId))
				{
					slot = slots[slotId];
					return true;
				}
				else
				{
					return false;
				}
			}

			return TryGetFreeSlot(equipmentData.SlotType, out slot);
		}

		/// <inheritdoc/>
		public bool TryEquip(RuntimeItemData runtimeItemData, out RuntimeEquipedData equipedData, string slotId = null)
		{
			// First make sure we can equip this item.
			if (CanEquip(runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> conflicts, slotId))
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

				// Occupy the slot, apply data and invoke equiped event.
				equipedItems[slot.ID] = equipedData;
				equipmentData.Set(slot.ID, equipedData.RuntimeItemData.RuntimeID);
				EquipedEvent?.Invoke(equipedData);

				return true;
			}

			SpaxDebug.Error("Couldn't equip item.", $"Item=({runtimeItemData})");
			equipedData = null;
			return false;
		}

		/// <inheritdoc/>
		public void Unequip(RuntimeEquipedData equipedData)
		{
			if (equipedItems.ContainsKey(equipedData.Slot.ID))
			{
				// Stop all running equiped behaviour.
				equipedData.StopBehaviour();

				if (equipedData.EquipedVisual != null)
				{
					// Destroy the equiped object.
					sharedRigHandler.Remove(equipedData.EquipedVisual);
					Destroy(equipedData.EquipedVisual);
				}

				// Clear equiped slot, apply data and invoke unequiping event.
				equipedItems.Remove(equipedData.Slot.ID);
				equipmentData.TryRemove(equipedData.Slot.ID, true);
				UnequipingEvent?.Invoke(equipedData);

				// Dispose of all data.
				IDependencyManager dependencyManager = equipedData.DependencyManager;
				equipedData.Dispose();
				dependencyManager.Dispose();
			}
		}

		/// <inheritdoc/>
		public List<RuntimeEquipedData> GetEquipedFromSlotType(string slotType)
		{
			return EquipedItems.Where(e => e.Slot.Type == slotType).ToList();
		}

		/// <inheritdoc/>
		public RuntimeEquipedData GetEquipedFromSlotID(string slot)
		{
			return EquipedItems.FirstOrDefault(e => e.Slot.ID == slot);
		}

		#endregion

		#region IInteractor

		/// <inheritdoc/>
		public override bool Able(string interactionType)
		{
			return interactionType == BaseInteractionTypes.EQUIP;
		}

		/// <inheritdoc/>
		protected override bool Attempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
		{
			// Create and execute interaction.
			interaction = new Interaction(interactionType, this, interactable, null,
				(IInteraction i, bool success) =>
				{
					if (success &&
						i.Data is IRuntimeItemData itemData &&
						itemData.ItemData is IEquipmentData equipmentData)
					{
						RuntimeItemData runtimeItemData = inventoryComponent.Inventory.AddItem(itemData);
						TryEquip(runtimeItemData, out _);
					}
					i.Dispose();
				});

			return interactable.TryInteract(interaction);
		}

		#endregion

		/// <summary>
		/// Instantiates the equipment data's prefab deactivated to allow for dependency injection afterwards.
		/// Returns null if there is no prefab to instantiate.
		/// </summary>
		private GameObject InstantiateEquipmentDeactivated(IEquipmentData equipmentData, IEquipmentSlot slot)
		{
			if (equipmentData.EquipedPrefab == null)
			{
				return null;
			}

			// Instantiate the equiped prefab in the correct parent.
			GameObject instance = DependencyUtils.InstantiateDeactivated(equipmentData.EquipedPrefab);
			Transform parent = slot.Parent == null ? Entity.GameObject.transform : slot.Parent;
			instance.transform.SetParent(parent);

			// Disable components that may possibly interfere.
			if (instance.TryGetComponentInChildren(out Rigidbody rigidbody))
			{
				rigidbody.isKinematic = true;
			}
			if (instance.TryGetComponentsInChildren(out Collider[] colliders))
			{
				foreach (Collider collider in colliders)
				{
					collider.enabled = false;
				}
			}

			// Apply orientation.
			(Vector3 pos, Quaternion rot) orientation = slot.GetOrientation();
			instance.transform.localPosition = orientation.pos;
			instance.transform.localRotation = orientation.rot;

			// Apply the agent's rig when possible.
			sharedRigHandler.Share(instance);

			return instance;
		}

		#region Item options

		private void OnRequestInventoryItemOptionsMsg(RequestOptionsMsg<RuntimeItemData> msg)
		{
			if (msg.Target.ItemData is IEquipmentData equipmentData)
			{
				Option equipOption = new Option(
					"Equip",
					$"Equips this item on an (available) '{equipmentData.SlotType}' slot.",
					(option) =>
					{
						TryEquip(msg.Target, out _);
					});

				msg.AddOption(equipOption);
			}
		}

		private void OnRequestEquipedItemOptionsMsg(RequestOptionsMsg<RuntimeEquipedData> msg)
		{
			Option equipOption = new Option(
				"Unequip",
				"Unequips this item.",
				(option) =>
				{
					Unequip(msg.Target);
				});

			msg.AddOption(equipOption);
		}

		#endregion
	}
}
