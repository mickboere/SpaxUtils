using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Items/ItemDataAsset")]
	public class ItemDataAsset : ScriptableObject, IItemData, IBindingKeyProvider
	{
		public const string TT_UNIQUE = "Unique items cannot be stacked, and will always count as a new data entry when added to the inventory.";

		/// <inheritdoc/>
		public object BindingKey => $"ITEM:{ID}";

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
		public IReadOnlyList<BehaviourAsset> InventoryBehaviour => inventoryBehaviour;

		/// <inheritdoc/>
		public IReadOnlyDictionary<string, float> FloatStats
		{
			get
			{
				if (_floatStats == null)
				{
					_floatStats = itemStats.ToDictionary((LabeledFloatData f) => f.ID, (LabeledFloatData f) => f.FloatValue);
				}
				return _floatStats;
			}
		}
		private Dictionary<string, float> _floatStats;

		[Header("Item Data")]
		[SerializeField] private string id;
		[SerializeField] private string itemName;
		[SerializeField, TextArea(3, 6)] private string itemDescription;
		[SerializeField, ConstDropdown(typeof(IItemCategoryConstants))] private string category;
		[SerializeField, Tooltip(TT_UNIQUE)] private bool unique;
		[SerializeField] private Sprite icon;
		[SerializeField] private GameObject worldItemPrefab;
		[SerializeField, Expandable] private List<BehaviourAsset> inventoryBehaviour;
		[SerializeField] private List<LabeledFloatData> itemStats;

		public override string ToString()
		{
			return $"ItemData\n{{\n\tID={ID};\n\tName={Name};\n\tDescription={Description};\n\tCategory={Category};\n}}";
		}
	}
}
