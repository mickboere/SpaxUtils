using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component wrapping around an <see cref="ItemInventory"/> in order to handle interactions and manage data loading.
	/// </summary>
	public class InventoryComponent : InteractorableComponent
	{
		public const string INVENTORY_DATA_ID = "Inventory";

		/// <summary>
		/// The inventory object containing all item data.
		/// </summary>
		public ItemInventory Inventory { get; private set; }

		/// <inheritdoc/>
		public override string[] InteractableTypes { get; protected set; } = new string[] { BaseInteractionTypes.INVENTORY };

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

		public override bool IsInteractable(string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		/// <summary>
		/// Attempts to retrieve <see cref="IRuntimeItemData"/> from the interaction data and adds it to the inventory.
		/// </summary>
		protected override bool OnTryInteract(IInteraction interaction)
		{
			if (interaction.Data is IRuntimeItemData itemData)
			{
				Inventory.AddItem(itemData);
				interaction.Conclude(true);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public override bool CanInteract(string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		protected override bool OnTryCreateInteraction(string interactionType, IInteractable interactable, out IInteraction interaction, object data = null)
		{
			SpaxDebug.Log($"OnAttempt {interactionType}");

			// Create and execute interaction.
			interaction = new Interaction(interactionType, this, interactable, null,
				(IInteraction i, bool success) =>
				{
					SpaxDebug.Log($"On interaction concluded {i.Success}");

					if (success && i.Data is IRuntimeItemData runtimeItemData)
					{
						SpaxDebug.Log($"AddItem: {runtimeItemData.ItemData.Name}");
						Inventory.AddItem(runtimeItemData);
					}
					i.Dispose();
				});

			return interactable.TryInteract(interaction);
		}
	}
}
