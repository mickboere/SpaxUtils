using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component wrapping around an <see cref="ItemInventory"/> in order to handle interactions and manage data loading.
	/// </summary>
	public class InventoryComponent : InteractableComponentBase, IInteractor
	{
		public const string INVENTORY_DATA_ID = "Inventory";

		/// <inheritdoc/>
		public override string InteractableType => InteractionTypes.INVENTORY;

		/// <summary>
		/// The inventory object containing all item data.
		/// </summary>
		public ItemInventory Inventory { get; private set; }

		private IDependencyManager dependencies;
		private IItemDatabase itemDatabase;
		private IItemData[] injectedItems;

		public void InjectDependencies(IDependencyManager dependencies, IItemDatabase itemDatabase,
			IItemData[] injectedItems)
		{
			this.dependencies = dependencies;
			this.itemDatabase = itemDatabase;
			this.injectedItems = injectedItems;
		}

		protected void Awake()
		{
			Inventory = new ItemInventory(dependencies, itemDatabase, Entity.RuntimeData.GetEntry(INVENTORY_DATA_ID, new RuntimeDataCollection(INVENTORY_DATA_ID)));

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
			Inventory.Dispose();
		}

		#region Interaction

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
				interaction.InitiatedEvent += ExtractItem;
				return true;
			}

			interaction = null;
			return false;
		}

		private void ExtractItem(IInteraction interaction)
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

		#endregion Interactable
	}
}
