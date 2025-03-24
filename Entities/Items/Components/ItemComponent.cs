using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRuntimeItemDataComponent"/> implementation, containing a reference to an <see cref="IRuntimeItemData"/>.
	/// Component implements <see cref="IInteractable"/>, once sucessfully interacted with the Entity will be destroyed.
	/// </summary>
	public class ItemComponent : InteractableComponentBase, IRuntimeItemDataComponent
	{
		/// <inheritdoc/>
		public RuntimeItemData RuntimeItemData { get; private set; }

		public override string InteractableType => InteractionTypes.ITEM;

		[SerializeField, Expandable] private ItemDataAsset itemDataAsset;

		[NonSerialized] private IItemData itemData;
		[NonSerialized] private RuntimeDataCollection runtimeData;

		protected virtual void OnValidate()
		{
			UpdateIdentification();
		}

		protected virtual void Start()
		{
			UpdateIdentification();
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public override bool TryInteract(IInteraction interaction)
		{
			interaction.Data = RuntimeItemData;
			gameObject.SetActive(false);
			return true;
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
