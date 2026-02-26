using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class CairnComponent : InteractableComponentBase
	{
		/// <inheritdoc/>
		public override string InteractableType => InteractionTypes.CAIRN;

		private CairnService cairnService;

		public void InjectDependencies(CairnService cairnService)
		{
			this.cairnService = cairnService;
		}

		/// <inheritdoc/>
		public override List<string> GetInteractions(IEntity interactor)
		{
			return new List<string>() { InteractionTypes.CAIRN_COLLECT };
		}

		/// <inheritdoc/>
		public override bool TryInteract(IInteraction interaction)
		{
			RuntimeDataCollection data = Entity.RuntimeData;
			string owner = data.GetValue<string>(EntityDataIdentifiers.ID);

			if (interaction.Interactor.ID != owner)
			{
				// Cannot interact with a cairn that isn't yours.
				return false;
			}

			// Apply lost EXP to interactor's stats, if any.
			float scaling = data.GetValue(EntityDataIdentifiers.SCALING, 1f);
			if (scaling > Mathf.Epsilon)
			{
				AgentStatHandler statHandler = interaction.Interactor.GetEntityComponent<AgentStatHandler>();
				foreach (EntityStat stat in statHandler.BodyExperience.Stats)
				{
					stat.BaseValue += data.GetValue<float>(stat.Identifier) * scaling;
				}
			}

			// Add lost items to interactor's inventory, if any.
			if (data.TryGetEntry(InventoryComponent.INVENTORY_DATA_ID, out RuntimeDataCollection items))
			{
				ItemInventory inventory = interaction.Interactor.GetEntityComponent<InventoryComponent>().Inventory;
				EquipmentComponent equipment = interaction.Interactor.GetEntityComponent<EquipmentComponent>();
				foreach (RuntimeDataCollection item in items.Data)
				{
					RuntimeItemData rid = inventory.AddItem(item.CloneCollection());

					// If item was equiped, try to equip it again if it doesn't overlap.
					if (rid.RuntimeData.GetValue(ItemDataIdentifiers.EQUIPED, false) &&
						equipment.CanEquip(rid, out _, out List<RuntimeEquipedData> overlap, overwrite: false) &&
						overlap.Count == 0)
					{
						equipment.TryEquip(rid, out _);
					}
				}
			}

			// Remove cairn.
			Entity.GameObject.SetActive(false);
			cairnService.DeleteCairn(data.ID);
			interaction.Conclude(true);
			return true;
		}
	}
}
