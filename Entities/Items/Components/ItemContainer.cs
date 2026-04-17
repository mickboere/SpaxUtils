using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interactable entity component that turns an inventory into a lootable container.
	/// </summary>
	[RequireComponent(typeof(InventoryComponent))]
	[DefaultExecutionOrder(10)]
	public class ItemContainer : InteractableComponentBase
	{
		public const string GENERATED = "Generated";
		public override string InteractableType => InteractionTypes.CONTAINER;
		public ItemInventory Inventory => inventoryComponent.Inventory;

		[SerializeField] private List<LootTable> loot;
		[SerializeField, Tooltip("Will regenerate every time a new cycle is initiated.")] private bool regenerate;
		[SerializeField, Conditional(nameof(regenerate))] private bool onlyRegenIfEmpty;

		private WorldService cycleService;
		private RandomService randomService;
		private InventoryComponent inventoryComponent;

		public void InjectDependencies(WorldService cycleService, RandomService randomService)
		{
			this.cycleService = cycleService;
			this.randomService = randomService;
		}

		protected void OnEnable()
		{
			inventoryComponent = GetComponent<InventoryComponent>();

			cycleService.NewCycleEvent += OnNewCycleEvent;
		}

		protected void OnDisable()
		{
			cycleService.NewCycleEvent -= OnNewCycleEvent;
		}

		#region Interactable

		public override List<string> GetInteractions(IEntity interactor)
		{
			return new List<string>() { InteractionTypes.CONTAINER_OPEN };
		}

		public override bool TryInteract(IInteraction interaction)
		{
			if (interaction.Action == InteractionTypes.CONTAINER_OPEN)
			{
				// For animation: subscribe to interaction.initiation and invoke method with animation.
				// UI menu must be handled on Interactor end.
				interaction.Data = inventoryComponent;
				return true;
			}

			return false;
		}

		#endregion Interactable

		/// <summary>
		/// (Re)generates the loot for this container, clearing and filling the <see cref="InventoryComponent"/> with fresh loot.
		/// </summary>
		public void GenerateLoot(int seed)
		{
			Inventory.ClearInventory();
			for (int i = 0; i < loot.Count; i++)
			{
				List<RuntimeItemData> generatedLoot = loot[i].GenerateLoot(seed.Combine(i));
				foreach (RuntimeItemData item in generatedLoot)
				{
					Inventory.AddItem(item);
				}
			}
			if (!regenerate)
			{
				Entity.RuntimeData.SetValue(GENERATED, true);
			}
		}

		private void OnNewCycleEvent(int cycle)
		{
			if ((regenerate || !Entity.RuntimeData.GetValue<bool>(GENERATED)) &&
				(!onlyRegenIfEmpty || Inventory.Entries.Count == 0))
			{
				GenerateLoot(randomService.GenerateSeed(Entity.ID, cycle));
			}
		}
	}
}
