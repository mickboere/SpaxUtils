using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Equipment", menuName = "ScriptableObjects/EquipmentDataAsset")]
	public class EquipmentDataAsset : ItemDataAsset, IEquipmentData
	{
		public string SlotType => slotType;
		public IReadOnlyList<string> CoversLocations => coversLocations;
		public GameObject EquipedPrefab => equipedPrefab;
		public IReadOnlyList<BehaviorAsset> EquipedBehaviour => equipedBehaviour;

		[Header("Equipment Data")]
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slotType;
		[SerializeField, ConstDropdown(typeof(IBodyLocationConstants))] private List<string> coversLocations;
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField] private List<BehaviorAsset> equipedBehaviour;
	}
}
