using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component wrapping around an <see cref="ItemInventory"/> in order to handle interactions and manage data loading.
	/// </summary>
	public class InventoryComponent : InteractableInteractorBase
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

		public void InjectDependencies(IDependencyManager dependencies, IItemDatabase itemDatabase)
		{
			this.dependencies = dependencies;
			this.itemDatabase = itemDatabase;
		}

		protected void Awake()
		{
			Inventory = new ItemInventory(dependencies, itemDatabase, new RuntimeDataCollection(INVENTORY_DATA_ID)); // TODO: should be loaded data
		}

		protected void OnDestroy()
		{
			Inventory.Dispose();
		}

		public override bool Supports(string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		/// <summary>
		/// Attempts to retrieve <see cref="IRuntimeItemData"/> from the interaction data and adds it to the inventory.
		/// </summary>
		protected override bool OnInteract(IInteraction interaction)
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
		public override bool Able(string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		protected override bool OnAttempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
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