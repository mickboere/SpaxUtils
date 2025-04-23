using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Item", menuName = "ScriptableObjects/Items/ItemDataAsset")]
	public class ItemDataAsset : ScriptableObject, IItemData, IBindingKeyProvider
	{
		public const string TT_UNIQUE = "Unique items cannot be stacked, and will always count as a new data entry when added to the inventory.";

		/// <inheritdoc/>
		public object BindingKey => $"ITEM:{ID}";

		/// <inheritdoc/>
		public string ID => Identification.ID;

		/// <inheritdoc/>
		public IIdentification Identification => id;

		/// <inheritdoc/>
		public string Description => itemDescription;

		/// <inheritdoc/>
		public bool Unique => unique;

		/// <inheritdoc/>
		public int Value => value;

		/// <inheritdoc/>
		public Sprite Icon => icon;

		public GameObject WorldItemPrefab => worldItemPrefab;

		/// <inheritdoc/>
		public IReadOnlyList<BehaviourAsset> InventoryBehaviour => inventoryBehaviour;

		/// <inheritdoc/>
		public RuntimeDataCollection Data
		{
			get
			{
				if (_data == null || _data.Data == null)
				{
					_data = data.ToRuntimeDataCollection(ID);
				}
				return _data;
			}
		}
		private RuntimeDataCollection _data;

		[Header("Item Data")]
		[SerializeField] private Identification id;
		[SerializeField, TextArea(3, 6)] private string itemDescription;
		[SerializeField, Tooltip(TT_UNIQUE)] private bool unique;
		[SerializeField] private int value = 1;
		[SerializeField] private Sprite icon;
		[SerializeField] private GameObject worldItemPrefab;
		[SerializeField, Expandable] private List<BehaviourAsset> inventoryBehaviour;
		[SerializeField] private LabeledDataCollection data;

		public override string ToString()
		{
			return $"ItemData\n{{\n\tID={id.TagFull()};\n\tDescription={Description};\n}}";
		}
	}
}
