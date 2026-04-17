using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(ConsumableContainerItemBehaviourAsset), menuName = "ScriptableObjects/Behaviours/" + nameof(ConsumableContainerItemBehaviourAsset))]
	public class ConsumableContainerItemBehaviourAsset : ConsumableItemBehaviourAsset, IContainerItem
	{
		public float Capacity => runtimeItemData.RuntimeData.GetValue(capacity, 1f);
		public float Contains => runtimeItemData.RuntimeData.GetValue(contains, 0f);
		public float FillAmount => Contains / Capacity;

		[Header("Container")]
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string capacity;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string contains;
		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string consume;
		[SerializeField, Tooltip("Will automatically refill the container when the agent is being recovered.")] private bool autoFill;

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
			runtimeItemData.RuntimeData.TryGetEntry(contains, out RuntimeDataEntry container, new RuntimeDataEntry(contains, 0f));
			container.Value = (float)container.Value - consume;
			ApplyStats(consume / Capacity);

			if (runtimeItemData.RuntimeData.TryGetEntry(this.consume, out RuntimeDataEntry c) && (bool)c.Value && Contains.Approx(0f))
			{
				inventory.Inventory.RemoveItem(runtimeItemData.RuntimeID);
			}
		}

		/// <inheritdoc/>
		public override bool CanConsume() => Contains > 0f;

		/// <inheritdoc/>
		public void Fill(float amount)
		{
			runtimeItemData.RuntimeData.TryGetEntry(contains, out RuntimeDataEntry container, new RuntimeDataEntry(contains, 0f));
			container.Value = Mathf.Min((float)container.Value + amount, Capacity);
		}

		private void OnRecover()
		{
			Fill(Capacity);
		}
	}
}
