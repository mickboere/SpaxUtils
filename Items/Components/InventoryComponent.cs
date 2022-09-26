using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component wrapping around an <see cref="ItemInventory"/> in order to handle interactions and manage data loading.
	/// </summary>
	public class InventoryComponent : EntityComponentBase, IInteractable, IInteractor
	{
		public const string INVENTORY_DATA_ID = "Inventory";

		/// <inheritdoc/>
		public IReadOnlyList<string> InteractableTypes => interactableTypes;

		/// <summary>
		/// The inventory data object containing all item data.
		/// </summary>
		public ItemInventory Inventory { get; private set; }

		private readonly List<string> interactableTypes = new List<string>() { BaseInteractionTypes.INVENTORY };

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

		#region IInteractionHandler

		/// <inheritdoc/>
		public bool Interactable(IInteractor interactor, string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		/// <summary>
		/// Attempts to retrieve <see cref="IRuntimeItemDataComponent"/> from the interaction data and adds it to the inventory.
		/// </summary>
		public bool Interact(IInteraction interaction)
		{
			if (!Interactable(interaction.Interactor, interaction.Type))
			{
				return false;
			}

			if (interaction.Data is IRuntimeItemDataComponent entityItemData)
			{
				Inventory.AddItem(entityItemData);
				interaction.Conclude(true);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public bool Able(string interactionType)
		{
			return interactionType == BaseInteractionTypes.INVENTORY;
		}

		/// <inheritdoc/>
		public bool Attempt(string interactionType, IInteractable interactable, object data, out IInteraction interaction)
		{
			// Ensure ability and interactability.
			interaction = null;
			if (!Able(interactionType) || !interactable.Interactable(this, interactionType))
			{
				return false;
			}

			// Create and execute interaction.
			interaction = new Interaction(interactionType, this, interactable, null,
				(IInteraction i, bool success) =>
				{
					if (success && i.Data is IRuntimeItemData runtimeItemData)
					{
						Inventory.AddItem(runtimeItemData);
					}
					i.Dispose();
				});

			return interactable.Interact(interaction);
		}

		#endregion
	}
}
