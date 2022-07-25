using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Equipment", menuName = "ScriptableObjects/EquipmentData")]
	public class EquipmentData : ItemData, IEquipmentData
	{
		public string SlotType => slotType;
		public IReadOnlyList<string> CoversLocations => coversLocations;
		public GameObject EquipedPrefab => equipedPrefab;
		public IReadOnlyList<BehaviourAsset> EquipedBehaviour => equipedBehaviour;

		[Header("Equipment Data")]
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants)), FormerlySerializedAs("equipSlot")] private string slotType;
		[SerializeField, ConstDropdown(typeof(IBodyLocationConstants))] private List<string> coversLocations;
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField] private List<BehaviourAsset> equipedBehaviour;
	}
}
