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
		public IReadOnlyList<BehaviourAsset> EquipedBehaviour => equipedBehaviour;
		public IReadOnlyList<StatMappingSheet> EquipedStatMappings => equipedStatMappings;

		private const string TT_SLOT_TYPE =
			"The type of slot this equipment must be equiped in." +
			"\nThe slot type decides the location and parenting for the \"equipedPrefab\".";
		private const string TT_COVER =
			"Defines which physical locations this equipment covers when equiped." +
			"\nUsed to avoid visual overlap by unequiping conflicting items.";

		[Header("Equipment Data")]
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField, Tooltip(TT_SLOT_TYPE), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slotType;
		[SerializeField, Tooltip(TT_COVER), ConstDropdown(typeof(IBodyLocationConstants))] private List<string> coversLocations;
		[SerializeField, Expandable] private List<BehaviourAsset> equipedBehaviour;
		[SerializeField, Expandable] private List<StatMappingSheet> equipedStatMappings;
	}
}
