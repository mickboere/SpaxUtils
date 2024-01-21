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
		/// <see cref="StatMappingSheet"/>s which map <see cref="IItemData.FloatStats"/> to the entity upon equiping.
		/// </summary>
		IReadOnlyList<StatMappingSheet> EquipedStatMappings { get; }
	}
}
