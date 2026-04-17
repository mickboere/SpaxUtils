using UnityEngine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface <see cref="IItemData"/> that is equipable.
	/// </summary>
	public interface IEquipmentData : IItemData
	{
		/// <summary>
		/// The prefab to instantiate when this equipment is equiped.
		/// </summary>
		GameObject EquipedPrefab { get; }

		/// <summary>
		/// Material override mappings applied to the equiped prefab instance.
		/// If an entry has Source == null, it acts as a wildcard for all non-matching materials.
		/// </summary>
		IReadOnlyList<MaterialOverride> MaterialOverrides { get; }

		/// <summary>
		/// The type of slot this equipment is able to be equiped in.
		/// </summary>
		string SlotType { get; }

		/// <summary>
		/// All locations that get covered when this equipment is equiped.
		/// </summary>
		IReadOnlyList<string> CoversLocations { get; }

		/// <summary>
		/// The behaviors to execute when this equipment is equiped.
		/// </summary>
		IReadOnlyList<BehaviourAsset> EquipedBehaviour { get; }

		/// <summary>
		/// <see cref="StatMap"/>s which map <see cref="IItemData.FloatStats"/> to the entity upon equiping.
		/// </summary>
		IReadOnlyList<StatMap> EquipedStatMappings { get; }

		/// <summary>
		/// Scaler for the final physics rating, can be used as "coverage" for apparel.
		/// </summary>
		float PhysicsScaling { get; }

		/// <summary>
		/// The normal distribution of this equipment's physics effects.
		/// </summary>
		Vector8 PhysicsDistribution { get; }
	}
}
