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

		[SerializeField] private SerializedItemData item;

		protected virtual void OnValidate()
		{
			UpdateIdentification();
		}

		protected virtual void Awake()
		{
			UpdateIdentification();
			RefreshRuntimeItemData();
		}

		/// <inheritdoc/>
		public override bool TryInteract(IInteraction interaction)
		{
			interaction.Data = RuntimeItemData;
			gameObject.SetActive(false);
			Entity.RuntimeData.SetValue(EntityDataIdentifiers.OFF, true);
			return true;
		}

		private void UpdateIdentification()
		{
			if (item != null && item.Asset != null && Entity != null)
			{
				Entity.Identification.Name = item.Asset.Identification.Name;
				Entity.Identification.Add(EntityLabels.ITEM);
			}
		}

		private void RefreshRuntimeItemData()
		{
			RuntimeItemData = item.ToRuntimeItemData();
		}
	}
}
