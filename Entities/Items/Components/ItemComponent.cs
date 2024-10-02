using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRuntimeItemDataComponent"/> implementation, containing a reference to an <see cref="IRuntimeItemData"/>.
	/// Component implements <see cref="IInteractable"/>, once sucessfully interacted with the Entity will be destroyed.
	/// </summary>
	public class ItemComponent : InteractableBase, IRuntimeItemDataComponent
	{
		/// <inheritdoc/>
		public IRuntimeItemData RuntimeItemData { get; private set; }

		/// <inheritdoc/>
		public override string[] InteractableTypes { get; protected set; } = new string[]
		{
			BaseInteractionTypes.INVENTORY,
			BaseInteractionTypes.EQUIP
		};

		[SerializeField, Expandable] private ItemDataAsset itemDataAsset;

		[NonSerialized] private IItemData itemData;
		[NonSerialized] private RuntimeDataCollection runtimeData;

		protected void OnValidate()
		{
			UpdateIdentification();
		}

		protected void Start()
		{
			UpdateIdentification();
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public void SetItemData(IItemData itemData)
		{
			this.itemData = itemData;
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public void SetRuntimeData(RuntimeDataCollection runtimeData)
		{
			this.runtimeData = runtimeData;
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public override bool Supports(string interactionType)
		{
			switch (interactionType)
			{
				case BaseInteractionTypes.INVENTORY:
				case BaseInteractionTypes.EQUIP:
					return true;
				default:
					return false;
			}
		}

		/// <inheritdoc/>
		protected override bool OnInteract(IInteraction interaction)
		{
			interaction.Data = RuntimeItemData;
			interaction.Conclude(true);
			Destroy(Entity.GameObject);
			return true;
		}

		private void UpdateIdentification()
		{
			if (itemDataAsset != null && Entity != null)
			{
				Entity.Identification.Name = itemDataAsset.Name;
				Entity.Identification.Add(EntityLabels.ITEM);
			}
		}

		private void RefreshRuntimeItemData()
		{
			RuntimeItemData = new RuntimeItemData(
				itemData ?? itemDataAsset,
				runtimeData ?? new RuntimeDataCollection(Guid.NewGuid().ToString()),
				null);
		}
	}
}
