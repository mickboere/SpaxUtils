using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Equipment", menuName = "ScriptableObjects/Items/EquipmentDataAsset")]
	public class EquipmentDataAsset : ItemDataAsset, IEquipmentData
	{
		public GameObject EquipedPrefab => equipedPrefab;
		public IReadOnlyList<MaterialOverride> MaterialOverrides => materialOverrides;
		public string SlotType => slotType;
		public string SheatheCategory => sheatheCategory;
		public IReadOnlyList<string> CoversLocations => equipedPrefab != null && equipedPrefab.TryGetComponent(out IEntityApparel a) ? a.Locations : new List<string>();
		public IReadOnlyList<BehaviourAsset> CarriedBehaviour => carriedBehaviour;
		public IReadOnlyList<BehaviourAsset> WieldedBehaviour => wieldedBehaviour;
		public IReadOnlyList<StatMap> EquipedStatMappings => equipedStatMappings;

		public float PhysicsScaling => physicsScaling;
		public Vector8 PhysicsDistribution => physicsDistribution.Vector8;
		public bool PhysicsPassive => physicsPassive;

		private const string TT_SLOT_TYPE =
			"The type of slot this equipment must be equiped in." +
			"\nThe slot type decides the location and parenting for the \"equipedPrefab\".";

		private const string TT_SHEATHE =
			"How this equipment is carried when it is not in hand." +
			"\nThe body plan decides which sheathe point each category maps to.";

		// Resources path to the octad naming the physics lanes; only used to label the distribution in the inspector.
		private const string PHYSICS_OCTAD = "Stats/Octads/BodyPhysicsOctad";

		[Header("Equipment Data")]
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField] private List<MaterialOverride> materialOverrides = new List<MaterialOverride>();
		[SerializeField, Tooltip(TT_SLOT_TYPE), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slotType;
		[SerializeField, Tooltip(TT_SHEATHE), ConstDropdown(typeof(ISheatheCategoryConstants))] private string sheatheCategory;
		[SerializeField, Expandable, Tooltip("Runs while carried, wielded or not.")] private List<BehaviourAsset> carriedBehaviour;
		[SerializeField, Expandable, Tooltip("Runs only while actively wielded (in hand).")] private List<BehaviourAsset> wieldedBehaviour;
		[SerializeField, Expandable] private List<StatMap> equipedStatMappings;
		[Header("Physics")]
		[SerializeField] private bool physicsPassive;
		[SerializeField, Range(0f, 1f)] private float physicsScaling = 1f;
		[SerializeField, OctadLabels(PHYSICS_OCTAD)] private RangedOctad physicsDistribution;
	}
}
