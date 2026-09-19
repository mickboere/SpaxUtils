using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Equipment", menuName = "ScriptableObjects/Items/EquipmentDataAsset")]
	public class EquipmentDataAsset : ItemDataAsset, IEquipmentData
	{
		public GameObject EquipedPrefab => equipedPrefab;
		public float Coverage => coverage;
		public string Surface => surface;
		public IReadOnlyList<MaterialOverride> MaterialOverrides => materialOverrides;
		public string SlotType => slotType;
		public IReadOnlyList<string> CoversLocations => equipedPrefab != null && equipedPrefab.TryGetComponent(out IEntityApparel a) ? a.Locations : new List<string>();
		public IReadOnlyList<BehaviourAsset> CarriedBehaviour => carriedBehaviour;
		public IReadOnlyList<BehaviourAsset> WieldedBehaviour => wieldedBehaviour;
		public IReadOnlyList<StatMap> EquipedStatMappings => equipedStatMappings;

		public Vector8 PhysicsDistribution => physicsDistribution.Vector8;
		public bool PhysicsPassive => physicsPassive;

		private const string TT_SLOT_TYPE =
			"The type of slot this equipment must be equiped in." +
			"\nThe slot type decides the location and parenting for the \"equipedPrefab\".";

		private const string TT_COVERAGE =
			"How much of the wearer this piece accounts for; a full set totals 1." +
			"\nScales its physics, and is its share of the surface mix when struck.";

		private const string TT_SURFACE =
			"What this piece sounds like when struck or striking." +
			"\nNULL contributes nothing, letting the body underneath speak in its place.";

		// Resources path to the octad naming the physics lanes; only used to label the distribution in the inspector.
		private const string PHYSICS_OCTAD = "Stats/Octads/BodyPhysicsOctad";

		[Header("Equipment Data")]
		[SerializeField, Tooltip(TT_SLOT_TYPE), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string slotType;
		[SerializeField] private GameObject equipedPrefab;
		[SerializeField, Range(0f, 1f), Tooltip(TT_COVERAGE), FormerlySerializedAs("physicsScaling")]
		private float coverage = 1f;
		[SerializeField, Tooltip(TT_SURFACE), ConstDropdown(typeof(ISurfaceTypeConstants), includeEmpty: true)]
		private string surface;
		[SerializeField] private List<MaterialOverride> materialOverrides = new List<MaterialOverride>();
		[SerializeField, Expandable, Tooltip("Runs while carried, wielded or not.")] private List<BehaviourAsset> carriedBehaviour;
		[SerializeField, Expandable, Tooltip("Runs only while actively wielded (in hand).")] private List<BehaviourAsset> wieldedBehaviour;
		[SerializeField, Expandable] private List<StatMap> equipedStatMappings;
		[Header("Physics")]
		[SerializeField] private bool physicsPassive;
		[SerializeField, OctadLabels(PHYSICS_OCTAD)] private RangedOctad physicsDistribution;
	}
}
