using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Equipment", menuName = "ScriptableObjects/Items/EquipmentDataAsset")]
	public class EquipmentDataAsset : ItemDataAsset, IEquipmentData
	{
		public GameObject EquipedPrefab => equipedPrefab;
		public string SlotType => slotType;
		public IReadOnlyList<string> CoversLocations => coversLocations;
		public IReadOnlyList<BehaviorAsset> EquipedBehaviour => equipedBehaviour;

		[Header("Equipment Data")]
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slotType;
		[SerializeField, ConstDropdown(typeof(IBodyLocationConstants))] private List<string> coversLocations;
		[SerializeField, Expandable] private List<BehaviorAsset> equipedBehaviour;
	}
}
