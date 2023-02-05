using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Items/ItemDataAsset")]
	public class ItemDataAsset : ScriptableObject, IItemData
	{
		public const string TT_UNIQUE = "Unique items cannot be stacked, and will always count as a new data entry when added to the inventory.";

		/// <inheritdoc/>
		public string ID => id;

		/// <inheritdoc/>
		public string Name => itemName;

		/// <inheritdoc/>
		public string Description => itemDescription;

		/// <inheritdoc/>
		public string Category => category;

		/// <inheritdoc/>
		public bool Unique => unique;

		/// <inheritdoc/>
		public Sprite Icon => icon;

		public GameObject WorldItemPrefab => worldItemPrefab;

		/// <inheritdoc/>
		public IReadOnlyList<BehaviorAsset> InventoryBehaviour => inventoryBehaviour;

		/// <inheritdoc/>
		public IReadOnlyList<LabeledFloatData> Stats => stats;

		[Header("Item Data")]
		[SerializeField] private string id;
		[SerializeField] private string itemName;
		[SerializeField, TextArea(3, 6)] private string itemDescription;
		[SerializeField, ConstDropdown(typeof(IItemCategoryConstants))] private string category;
		[SerializeField, Tooltip(TT_UNIQUE)] private bool unique;
		[SerializeField] private Sprite icon;
		[SerializeField] private GameObject worldItemPrefab;
		[SerializeField, Expandable] private List<BehaviorAsset> inventoryBehaviour;
		[SerializeField] private List<LabeledFloatData> stats;

		public override string ToString()
		{
			return $"ItemData\n{{\n\tID={ID};\n\tName={Name};\n\tDescription={Description};\n\tCategory={Category};\n}}";
		}
	}
}
