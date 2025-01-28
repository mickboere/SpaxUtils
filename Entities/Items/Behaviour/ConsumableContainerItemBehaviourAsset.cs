using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(ConsumableContainerItemBehaviourAsset), menuName = "ScriptableObjects/Behaviours/" + nameof(ConsumableContainerItemBehaviourAsset))]
	public class ConsumableContainerItemBehaviourAsset : ConsumableItemBehaviourAsset, IContainerItem
	{
		public float Capacity => runtimeItemData.GetStat(capacity, true, 1f);
		public float Contains => runtimeItemData.GetStat(contains, true, 0f);
		public float FillAmount => Contains / Capacity;

		[Header("Container")]
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string capacity;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string contains;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string consume;
		[SerializeField] private bool autoFill;

		public override void Start()
		{
			base.Start();

			if (autoFill)
			{
				agent.RecoverEvent += OnRecover;
			}
		}

		public override void Stop()
		{
			base.Stop();

			if (autoFill)
			{
				agent.RecoverEvent -= OnRecover;
			}
		}

		/// <inheritdoc/>
		public override void Consume(float amount)
		{
			float consume = Mathf.Min(Contains, amount);
			runtimeItemData.TryGetData(contains, out RuntimeDataEntry container, true, 0f);
			container.Value = (float)container.Value - consume;
			ApplyStats(consume / Capacity);

			if (runtimeItemData.TryGetData(this.consume, out RuntimeDataEntry c) && (bool)c.Value && Contains.Approx(0f))
			{
				inventory.Inventory.RemoveItem(runtimeItemData.RuntimeID);
			}
		}

		/// <inheritdoc/>
		public void Fill(float amount)
		{
			SpaxDebug.Log("Fill");
			runtimeItemData.TryGetData(contains, out RuntimeDataEntry container, true, 0f);
			container.Value = Mathf.Min((float)container.Value + amount, Capacity);
		}

		private void OnRecover()
		{
			Fill(Capacity);
		}
	}
}
