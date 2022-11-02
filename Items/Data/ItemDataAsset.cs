using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/ItemDataAsset")]
	public class ItemDataAsset : ScriptableObject, IItemData
	{
		/// <inheritdoc/>
		public string UID => id;

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

		[Header("Item Data")]
		[SerializeField] private string id;
		[SerializeField] private string itemName;
		[SerializeField, TextArea(3, 6)] private string itemDescription;
		[SerializeField, ConstDropdown(typeof(IItemCategoryConstants))] private string category;
		[SerializeField, Tooltip("Unique items cannot be stacked, and will always count as a new data entry when added to the inventory.")] private bool unique;
		[SerializeField] private Sprite icon;
		[SerializeField] private GameObject worldItemPrefab;
		[SerializeField] private List<BehaviorAsset> inventoryBehaviour;

		public override string ToString()
		{
			return $"ItemData\n{{\n\tID={UID};\n\tName={Name};\n\tDescription={Description};\n\tCategory={Category};\n}}";
		}
	}
}
