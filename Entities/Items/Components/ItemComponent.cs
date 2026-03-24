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
		[SerializeField, HideInInspector] private int lastItemDataHash;

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
			if (item == null || item.Asset == null || Entity == null)
			{
				return;
			}

			// Always keep label.
			Entity.Identification.Add(EntityLabels.ITEM);

#if UNITY_EDITOR
			// Only allow ID resets in the editor, not during play.
			if (!Application.isPlaying)
			{
				int currentHash = JsonUtility.ToJson(item).GetHashCode();
				if (lastItemDataHash != currentHash)
				{
					lastItemDataHash = currentHash;
					Entity.Identification.Name = item.Asset.Identification.Name;
					Entity.Identification.ID = Guid.NewGuid().ToString();
				}
				else if (Entity.Identification.Name != item.Asset.Identification.Name)
				{
					// Item data didn't change but the asset's name was updated; sync name without resetting ID.
					Entity.Identification.Name = item.Asset.Identification.Name;
				}
			}
#else
			// In builds/play mode: never touch ID. Optionally sync name only.
			if (Entity.Identification.Name != item.Asset.Identification.Name)
			{
				Entity.Identification.Name = item.Asset.Identification.Name;
			}
#endif
		}

		private void RefreshRuntimeItemData()
		{
			RuntimeItemData = item.ToRuntimeItemData();
		}
	}
}
