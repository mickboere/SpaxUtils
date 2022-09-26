using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Simple editor-configurable <see cref="IRuntimeItemDataComponent"/> implementation that does not contain any <see cref="RuntimeData"/> by default.
	/// </summary>
	public class WorldItemComponent : EntityComponentBase, IRuntimeItemDataComponent, IInteractable
	{
		/// <inheritdoc/>
		public IItemData ItemData { get; private set; }

		/// <inheritdoc/>
		public RuntimeDataCollection RuntimeData => runtimeData;

		/// <inheritdoc/>
		public IReadOnlyList<string> InteractableTypes => interactableTypes;

		//public int NamingPriority => 1;

		[SerializeField] private ItemData itemData;
		[SerializeField] private RuntimeDataCollection runtimeData;

		private readonly List<string> interactableTypes = new List<string>()
		{
			BaseInteractionTypes.INVENTORY,
			BaseInteractionTypes.EQUIP
		};

		protected void OnValidate()
		{
			UpdateIdentification();
		}

		protected void Start()
		{
			UpdateIdentification();

			if (ItemData == null)
			{
				ItemData = itemData;
			}
		}

		public bool Interactable(IInteractor interactor, string interactionType)
		{
			return interactableTypes.Contains(interactionType);
		}

		public bool Interact(IInteraction interaction)
		{
			if (!Interactable(interaction.Interactor, interaction.Type))
			{
				interaction.Conclude(false);
				return false;
			}

			interaction.Data = new RuntimeItemDataStruct(ItemData, RuntimeData);
			Destroy(Entity.GameObject);
			return true;
		}

		public void SetRuntimeData(RuntimeDataCollection runtimeData)
		{
			this.runtimeData = runtimeData;
		}

		private void UpdateIdentification()
		{
			if (itemData != null && Entity != null)
			{
				Entity.Identification.Name = itemData.Name;
				//Entity.Identification.Add(EntityLabels.ITEM);
			}
		}
	}
}
