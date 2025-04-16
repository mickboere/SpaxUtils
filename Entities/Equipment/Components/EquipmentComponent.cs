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
	public class EquipmentComponent : InteractorComponentBase
	{
		public const string EQUIPMENT_DATA_ID = "Equipment";

		public event Action<RuntimeEquipedData> EquipedEvent;
		public event Action<RuntimeEquipedData> UnequipingEvent;

		/// <summary>
		/// All registered <see cref="IEquipmentSlot"/>s.
		/// </summary>
		public IReadOnlyCollection<IEquipmentSlot> Slots => slots.Values;

		/// <summary>
		/// All equiped <see cref="RuntimeEquipedData"/>.
		/// </summary>
		public IReadOnlyCollection<RuntimeEquipedData> EquipedItems => equipedItems.Values;

		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string[] defaultSlots;

		private EntityAppearanceHandler appearanceHandler;
		private ICommunicationChannel comms;
		private InventoryComponent inventoryComponent;
		private IEquipmentData[] injectedEquipment;

		private RuntimeDataCollection equipmentData;
		private Dictionary<string, IEquipmentSlot> slots = new Dictionary<string, IEquipmentSlot>();
		private Dictionary<string, RuntimeEquipedData> equipedItems = new Dictionary<string, RuntimeEquipedData>(); // string = slot.UID

		public void InjectDependencies(EntityAppearanceHandler sharedRigHandler,
			ICommunicationChannel comms, InventoryComponent inventoryComponent,
			IEquipmentData[] injectedEquipment)
		{
			this.appearanceHandler = sharedRigHandler;
			this.comms = comms;
			this.inventoryComponent = inventoryComponent;
			this.injectedEquipment = injectedEquipment;
		}

		protected void Awake()
		{
			for (int i = 0; i < defaultSlots.Length; i++)
			{
				AddSlot(new EquipmentSlot(i.ToString(), defaultSlots[i]));
			}
		}

		protected void Start()
		{
			if (Entity.RuntimeData.ContainsEntry(EQUIPMENT_DATA_ID))
			{
				// Load existing equipment data and load it.
				equipmentData = Entity.RuntimeData.GetEntry<RuntimeDataCollection>(EQUIPMENT_DATA_ID);
				foreach (RuntimeDataEntry entry in equipmentData.Data)
				{
					TryEquip(inventoryComponent.Inventory.Get((string)entry.Value), out _, entry.ID);
				}
			}
			else
			{
				// Create new equipment data and equip injected data, if any.
				equipmentData = new RuntimeDataCollection(EQUIPMENT_DATA_ID);
				Entity.RuntimeData.TryAdd(equipmentData);
				foreach (IEquipmentData equipment in injectedEquipment)
				{
					RuntimeItemData itemData = inventoryComponent.Inventory.Get(equipment);
					if (itemData != null)
					{
						TryEquip(itemData, out _);
					}
				}
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

		#region Slot Management

		public bool TryGetSlotFromID(string id, out IEquipmentSlot slot)
		{
			slot = null;
			if (slots.ContainsKey(id))
			{
				slot = slots[id];
			}

			return slot != null;
		}

		public bool TryGetSlotFromType(string type, out IEquipmentSlot slot, Func<IEquipmentSlot, bool> predicate = null)
		{
			foreach (IEquipmentSlot s in Slots)
			{
				if (s.Type == type && (predicate == null || predicate(s)))
				{
					slot = s;
					return true;
				}
			}

			slot = null;
			return false;
		}

		public IEquipmentSlot AddNewSlot(string type, string id = null)
		{
			if (string.IsNullOrEmpty(id))
			{
				id = Guid.NewGuid().ToString();
			}
			else if (TryGetSlotFromID(id, out IEquipmentSlot slot))
			{
				return slot;
			}

			slots.Add(id, new EquipmentSlot(id, type));
			return slots[id];
		}

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

		public bool RemoveSlot(string id)
		{
			if (TryGetSlotFromID(id, out IEquipmentSlot slot))
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

		#endregion Slot Management

		#region Equipment

		/// <summary>
		/// Returns whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.
		/// </summary>
		/// <param name="runtimeItemData">The <see cref="RuntimeItemData"/> to check.</param>
		/// <param name="slot">The free equipment slot in which the equipment can be equiped.</param>
		/// <param name="overlap">The currently equiped data with which there is location overlap.
		/// Overlaps do no block equiping and will automatically be unblocked .</param>
		/// <param name="slotId">Optional specific slot ID, leave null to check all slots.</param>
		/// <param name="overwrite">Will allow one to equip on occupied slots.</param>
		/// <returns>Whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.</returns>
		public bool CanEquip(RuntimeItemData runtimeItemData,
			out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap,
			string slotId = null, bool overwrite = false)
		{
			return CanEquip(runtimeItemData, out slot, out overlap, out _, slotId, overwrite);
		}

		/// <summary>
		/// Returns whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.
		/// </summary>
		/// <param name="runtimeItemData">The <see cref="RuntimeItemData"/> to check.</param>
		/// <param name="slot">The free equipment slot in which the equipment can be equiped.</param>
		/// <param name="overlap">The currently equiped data with which there is location overlap.
		/// Overlaps do no block equiping and will automatically be unblocked .</param>
		/// <param name="reason">The reason the equipment cannot be equiped, if any.</param>
		/// <param name="slotId">Optional specific slot ID, leave null to check all slots.</param>
		/// <param name="overwrite">Will allow one to equip on occupied slots.</param>
		/// <returns>Whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.</returns>
		public bool CanEquip(RuntimeItemData runtimeItemData,
			out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap, out string reason,
			string slotId = null, bool overwrite = false)
		{
			slot = null;

			// Make sure item data is equipment to begin with.
			if (runtimeItemData.ItemData is not IEquipmentData equipmentData)
			{
				overlap = new List<RuntimeEquipedData>();
				reason = "Item data does not implement IEquipmentData.";
				return false;
			}

			// Overlap occurs when two items cover the same location(s), but they won't block equiping.
			overlap = EquipedItems.Where((e) => e.EquipmentData.CoversLocations.Any((c) => equipmentData.CoversLocations.Contains(c))).ToList();

			// Check if supplied slotID is available.
			if (slotId != null)
			{
				if (slots.ContainsKey(slotId))
				{
					slot = slots[slotId];
					reason = $"Couldn't overwrite item in slot '{slotId}'";
					return !equipedItems.ContainsKey(slotId) || overwrite;
				}
				else
				{
					reason = $"No slot exists for '{slotId}'";
					return false;
				}
			}

			reason = $"Slot could not be retrieved for type: {equipmentData.SlotType}";
			bool foundSlot = TryGetSlotFromType(equipmentData.SlotType, out slot, (s) => !equipedItems.ContainsKey(s.ID));
			if (!foundSlot && overwrite)
			{
				return TryGetSlotFromType(equipmentData.SlotType, out slot, null);
			}
			return foundSlot;
		}

		/// <summary>
		/// Tries to equip the given <paramref name="runtimeItemData"/>.
		/// </summary>
		public bool TryEquip(RuntimeItemData runtimeItemData, out RuntimeEquipedData equipedData, string slotId = null)
		{
			// First make sure we can equip this item.
			if (CanEquip(runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap, out string reason, slotId, true))
			{
				// Ensure the item is present within the entity's inventory.
				if (!inventoryComponent.Inventory.Contains(runtimeItemData))
				{
					runtimeItemData = inventoryComponent.Inventory.AddItem(runtimeItemData);
				}

				IEquipmentData itemData = runtimeItemData.ItemData as IEquipmentData;

				// If slot is occupied, unequip it first.
				if (equipedItems.ContainsKey(slot.ID))
				{
					Unequip(equipedItems[slot.ID]);
				}

				// Unequip equipment with overlapping locations.
				foreach (RuntimeEquipedData conflict in overlap)
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
				equipedData.InitializeBehaviour();

				// Occupy the slot, apply data and invoke equiped event.
				equipedItems[slot.ID] = equipedData;
				equipmentData.SetValue(slot.ID, equipedData.RuntimeItemData.RuntimeID);
				slot.Equip(equipedData);
				EquipedEvent?.Invoke(equipedData);

				return true;
			}

			SpaxDebug.Error("Couldn't equip item.", $"Reason: {reason}\nItem=({runtimeItemData})");
			equipedData = null;
			return false;
		}

		/// <summary>
		/// Unequips the given <see cref="RuntimeEquipedData"/>.
		/// </summary>
		public void Unequip(RuntimeEquipedData equipedData)
		{
			if (equipedItems.ContainsKey(equipedData.Slot.ID))
			{
				// Stop all running equiped behaviour.
				equipedData.StopBehaviour();

				if (equipedData.EquipedInstance != null)
				{
					// Destroy the equiped object.
					appearanceHandler.Remove(equipedData.EquipedInstance);
					Destroy(equipedData.EquipedInstance);
				}

				// Clear equiped slot, apply data and invoke unequiping event.
				equipedItems.Remove(equipedData.Slot.ID);
				equipmentData.TryRemove(equipedData.Slot.ID, true);
				equipedData.Slot.Unequip(equipedData);
				UnequipingEvent?.Invoke(equipedData);

				// Dispose of all data.
				IDependencyManager dependencyManager = equipedData.DependencyManager;
				equipedData.Dispose();
				dependencyManager.Dispose();
			}
		}

		/// <summary>
		/// Returns all items equiped on a slot of type <paramref name="slotType"/>
		/// </summary>
		/// <param name="slotType">The type of slot to retrieve the equiped items from.</param>
		/// <returns>All items equiped on a slot of type <paramref name="slotType"/></returns>
		public List<RuntimeEquipedData> GetEquipedFromSlotType(string slotType)
		{
			return EquipedItems.Where(e => e.Slot.Type == slotType).ToList();
		}

		/// <summary>
		/// Returns the <see cref="RuntimeEquipedData"/> stored in <paramref name="slot"/>, if any.
		/// </summary>
		/// <param name="slot">The UID of the <see cref="IEquipmentSlot"/> to retrieve the <see cref="RuntimeEquipedData"/> from.</param>
		/// <returns>The <see cref="RuntimeEquipedData"/> stored in <paramref name="slot"/>, if any.</returns>
		public RuntimeEquipedData GetEquipedFromSlotID(string slot)
		{
			return EquipedItems.FirstOrDefault(e => e.Slot.ID == slot);
		}

		#endregion Equipment

		#region IInteractor

		public override List<string> GetInteractions(IInteractable interactable)
		{
			if (interactable.InteractableType == InteractionTypes.ITEM &&
				interactable is IRuntimeItemDataComponent ridc &&
				ridc.RuntimeItemData.ItemData is IEquipmentData)
			{
				return new List<string>() { InteractionTypes.ITEM_EQUIP };
			}

			return new List<string>();
		}

		public override bool TryCreateInteraction(IInteractable interactable, string action, out IInteraction interaction)
		{
			if (interactable.InteractableType == InteractionTypes.ITEM &&
				action == InteractionTypes.ITEM_EQUIP)
			{
				interaction = new Interaction(Entity, interactable, action);
				interaction.InitiatedEvent += ExtractItem;
				return true;
			}

			interaction = null;
			return false;
		}

		private void ExtractItem(IInteraction interaction)
		{
			if (interaction.Data is RuntimeItemData itemData &&
				itemData.ItemData is IEquipmentData)
			{
				RuntimeItemData runtimeItemData = inventoryComponent.Inventory.AddItem(itemData);
				TryEquip(runtimeItemData, out _);
				interaction.Conclude(true);
			}
			else
			{
				interaction.Conclude(false);
			}
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
			GameObject instance = DependencyUtils.InstantiateDeactivated(equipmentData.EquipedPrefab, Entity.GameObject.transform);

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

			// Apply the agent's rig when possible.
			appearanceHandler.Add(instance);

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
						if (TryEquip(msg.Target, out _) &&
							msg.Context == ContextIdentifiers.ITEM_CONTAINER)
						{
							// Item belongs to a container, dispose it to remove it.
							msg.Target.Dispose();
						}
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
