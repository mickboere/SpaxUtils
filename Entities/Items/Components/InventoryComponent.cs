using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component wrapping around an <see cref="ItemInventory"/> in order to handle interactions and manage data loading.
	/// </summary>
	public class InventoryComponent : InteractableComponentBase, IInteractor
	{
		public const string INVENTORY_DATA_ID = "INVENTORY";

		/// <inheritdoc/>
		public override string InteractableType => InteractionTypes.INVENTORY;

		/// <summary>
		/// The inventory object containing all item data.
		/// </summary>
		public ItemInventory Inventory { get; private set; }

		/// <summary>
		/// Invoked when one or more item notifications have been enqueued.
		/// </summary>
		public event Action NotificationsChangedEvent;

		/// <summary>
		/// The number of pending item notifications in the queue.
		/// </summary>
		public int PendingNotificationCount => notifications.Count;

		private IDependencyManager dependencies;
		private IItemDatabase itemDatabase;
		private IItemData[] injectedItems;
		private Queue<ItemNotification> notifications = new Queue<ItemNotification>();

		public void InjectDependencies(IDependencyManager dependencies, IItemDatabase itemDatabase,
			IItemData[] injectedItems)
		{
			this.dependencies = dependencies;
			this.itemDatabase = itemDatabase;
			this.injectedItems = injectedItems;
		}

		protected void Awake()
		{
			Inventory = new ItemInventory(
				dependencies,
				itemDatabase,
				Entity.RuntimeData.GetEntry(INVENTORY_DATA_ID, new RuntimeDataCollection(INVENTORY_DATA_ID)),
				Entity is IAgent); // Only run behaviours if the entity is an agent.

			Inventory.QuantityChangedEvent += OnQuantityChanged;

			// Ensure injected item data is present.
			foreach (IItemData item in injectedItems)
			{
				if (Inventory.Get(item) == null)
				{
					Inventory.AddItem(item);
				}
			}
		}

		protected void OnDestroy()
		{
			Inventory.QuantityChangedEvent -= OnQuantityChanged;
			Inventory.Dispose();
		}

		/// <summary>
		/// Tries to dequeue the next item notification.
		/// If the queue exceeds <paramref name="overflowThreshold"/>, collapses all pending notifications
		/// into a single summary entry and clears the queue.
		/// </summary>
		/// <param name="overflowThreshold">Maximum queue size before collapsing into a summary.</param>
		/// <param name="notification">The dequeued notification.</param>
		/// <returns>True if a notification was dequeued, false if the queue was empty.</returns>
		public bool TryDequeueNotification(int overflowThreshold, out ItemNotification notification)
		{
			if (notifications.Count == 0)
			{
				notification = default;
				return false;
			}

			if (notifications.Count > overflowThreshold)
			{
				int count = notifications.Count;
				notifications.Clear();
				notification = new ItemNotification($"{count} New Items", null, count);
				return true;
			}

			notification = notifications.Dequeue();
			return true;
		}

		private void OnQuantityChanged(RuntimeItemData runtimeItemData, int delta)
		{
			notifications.Enqueue(new ItemNotification(
				runtimeItemData.Name,
				runtimeItemData.ItemData.Icon,
				delta));
			NotificationsChangedEvent?.Invoke();
		}

		#region Interactor

		/// <inheritdoc/>
		public List<string> GetInteractions(IInteractable interactable)
		{
			if (interactable.InteractableType == InteractionTypes.ITEM)
			{
				return new List<string>() { InteractionTypes.ITEM_TAKE };
			}
			return new List<string>();
		}

		/// <inheritdoc/>
		public bool TryCreateInteraction(IInteractable interactable, string action, out IInteraction interaction)
		{
			if (interactable.InteractableType == InteractionTypes.ITEM &&
				action == InteractionTypes.ITEM_TAKE)
			{
				interaction = new Interaction(Entity, interactable, action);
				interaction.InitiatedEvent += ExtractItemFromInteraction;
				return true;
			}

			interaction = null;
			return false;
		}

		private void ExtractItemFromInteraction(IInteraction interaction)
		{
			if (Inventory.TryAddItem(interaction.Data))
			{
				interaction.Conclude(true);
			}
			else
			{
				SpaxDebug.Error("Invalid item data!", $"Type={interaction.Data.GetType().FullName}.\nData={interaction.Data}.");
				interaction.Conclude(false);
			}
		}

		#endregion Interactor

		#region Interactable

		/// <inheritdoc/>
		//public override List<string> GetInteractions(IEntity interactor)
		//{
		//	return new List<string>() { InteractionTypes.INVENTORY_GIVE };
		//}

		/// <inheritdoc/>
		//public override bool TryInteract(IInteraction interaction)
		//{
		//	if (interaction.Action == InteractionTypes.INVENTORY_GIVE)
		//	{
		//		ExtractItem(interaction);
		//		return true;
		//	}
		//	return false;
		//}

		#endregion Interactable
	}
}
