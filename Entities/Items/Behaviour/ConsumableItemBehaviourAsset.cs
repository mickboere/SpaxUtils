using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(ConsumableItemBehaviourAsset), menuName = "ScriptableObjects/Behaviours/" + nameof(ConsumableItemBehaviourAsset))]
	public class ConsumableItemBehaviourAsset : BehaviourAsset, IConsumableItem, IOptionProvider<RuntimeItemData>
	{
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string[] stats;

		protected IAgent agent;
		protected RuntimeItemData runtimeItemData;
		protected InventoryComponent inventory;

		public void InjectDependencies(IAgent agent, RuntimeItemData runtimeItemData, InventoryComponent inventory)
		{
			this.agent = agent;
			this.runtimeItemData = runtimeItemData;
			this.inventory = inventory;
		}

		/// <inheritdoc/>
		public virtual void Consume(float amount = 1f)
		{
			amount = Mathf.Min(amount, runtimeItemData.Quantity);
			int newQuantity = runtimeItemData.Quantity - amount.FloorToInt();
			runtimeItemData.RuntimeData.SetValue(ItemDataIdentifier.QUANTITY, newQuantity);

			ApplyStats(amount);

			if (newQuantity == 0)
			{
				inventory.Inventory.RemoveItem(runtimeItemData.RuntimeID);
			}
		}

		/// <inheritdoc/>
		public virtual bool CanConsume() => true;

		protected void ApplyStats(float amount)
		{
			foreach (string stat in stats)
			{
				if (runtimeItemData.TryGetStat(stat, out float value) &&
					agent.RuntimeData.TryGetEntry(stat, out RuntimeDataEntry data))
				{
					data.Value = (float)data.Value + value * amount;
				}
			}
		}

		public void RequestOptions(IRequestOptionsMsg<RuntimeItemData> request)
		{
			if (request.Context == ContextIdentifiers.INVENTORY && CanConsume())
			{
				request.AddOption(new Option("Consume", "", (_) => Consume()));
			}
		}
	}
}
