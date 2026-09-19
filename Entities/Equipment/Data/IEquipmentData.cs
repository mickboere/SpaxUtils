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
		/// Behaviours that run for as long as this equipment is carried, wielded or not.
		/// </summary>
		IReadOnlyList<BehaviourAsset> CarriedBehaviour { get; }

		/// <summary>
		/// Behaviours that run only while this equipment is actively wielded (in hand).
		/// </summary>
		IReadOnlyList<BehaviourAsset> WieldedBehaviour { get; }

		/// <summary>
		/// <see cref="StatMap"/>s which map <see cref="IItemData.FloatStats"/> to the entity upon equiping.
		/// </summary>
		IReadOnlyList<StatMap> EquipedStatMappings { get; }

		/// <summary>
		/// How much of the wearer this piece accounts for; a full set totals 1.
		/// Scales its physics rating, and is its share of the surface mix when struck.
		/// </summary>
		float Coverage { get; }

		/// <summary>
		/// Surface type this piece sounds like; empty contributes nothing to the mix.
		/// </summary>
		string Surface { get; }

		/// <summary>
		/// The normal distribution of this equipment's physics effects.
		/// </summary>
		Vector8 PhysicsDistribution { get; }

		/// <summary>
		/// When true, <see cref="PhysicsDistribution"/> is applied passively to body stats on equip.
		/// When false, physics contribution is driven by combat behaviours during active use (weapons).
		/// </summary>
		bool PhysicsPassive { get; }
	}
}
