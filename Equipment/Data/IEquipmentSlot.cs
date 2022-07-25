using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for equipment slots, defining the type of slot and a unique ID used to reference this specific slot.
	/// </summary>
	public interface IEquipmentSlot : IUnique
	{
		/// <summary>
		/// String defining the type of equipment to be allowed in this slot.
		/// </summary>
		string Type { get; }

		/// <summary>
		/// An optional parent override in case this slot is for a unique location.
		/// Leave null to spawn equipment on agent root level.
		/// </summary>
		Transform Parent { get; }

		/// <summary>
		/// Returns the desired local orientation for equipment in this slot.
		/// </summary>
		/// <returns>The desired local orientation for equipment in this slot.</returns>
		(Vector3 pos, Quaternion rot) GetOrientation();
	}
}
