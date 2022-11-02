using UnityEngine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface <see cref="IItemData"/> that is equipable.
	/// </summary>
	public interface IEquipmentData : IItemData
	{
		string SlotType { get; }
		IReadOnlyList<string> CoversLocations { get; }
		GameObject EquipedPrefab { get; }
		IReadOnlyList<BehaviorAsset> EquipedBehaviour { get; }
	}
}
