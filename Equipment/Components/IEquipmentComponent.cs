using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Component that handles available <see cref="IEquipmentSlot"/>s and equiped <see cref="RuntimeEquipedData"/>.
	/// </summary>
	public interface IEquipmentComponent
	{
		event Action<RuntimeEquipedData> EquipedEvent;
		event Action<RuntimeEquipedData> UnequipingEvent;

		/// <summary>
		/// All registered <see cref="IEquipmentSlot"/>s.
		/// </summary>
		IReadOnlyCollection<IEquipmentSlot> Slots { get; }

		/// <summary>
		/// All equiped <see cref="RuntimeEquipedData"/>.
		/// </summary>
		IReadOnlyCollection<RuntimeEquipedData> EquipedItems { get; }

		bool TryGetSlot(string id, out IEquipmentSlot slot);

		bool TryGetFreeSlot(string type, out IEquipmentSlot slot);

		IEquipmentSlot AddNewSlot(string type, string id = null);

		bool AddSlot(IEquipmentSlot slot);

		bool RemoveSlot(string id);

		/// <summary>
		/// Returns whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.
		/// </summary>
		/// <param name="runtimeItemData">The <see cref="RuntimeItemData"/> to check.</param>
		/// <param name="slot">The free equipment slot in which the equipment can be equiped.</param>
		/// <param name="overlap">The currently equiped data with which there is location overlap.
		/// Overlaps do no block equiping and will automatically be unblocked .</param>
		/// <returns>Whether the given <paramref name="runtimeItemData"/> can be equiped on a free spot.</returns>
		bool CanEquip(RuntimeItemData runtimeItemData, out IEquipmentSlot slot, out List<RuntimeEquipedData> overlap);

		/// <summary>
		/// Tries to equip the given <paramref name="runtimeItemData"/>.
		/// </summary>
		bool TryEquip(RuntimeItemData runtimeItemData, out RuntimeEquipedData equipedData);

		/// <summary>
		/// Unequips the given <see cref="RuntimeEquipedData"/>.
		/// </summary>
		void Unequip(RuntimeEquipedData equipedData);

		/// <summary>
		/// Returns all items equiped on a slot of type <paramref name="slotType"/>
		/// </summary>
		/// <param name="slotType">The type of slot to retrieve the equiped items from.</param>
		/// <returns>All items equiped on a slot of type <paramref name="slotType"/></returns>
		List<RuntimeEquipedData> GetEquipedFromSlotType(string slotType);

		/// <summary>
		/// Returns the <see cref="RuntimeEquipedData"/> stored in <paramref name="slot"/>, if any.
		/// </summary>
		/// <param name="slot">The UID of the <see cref="IEquipmentSlot"/> to retrieve the <see cref="RuntimeEquipedData"/> from.</param>
		/// <returns>The <see cref="RuntimeEquipedData"/> stored in <paramref name="slot"/>, if any.</returns>
		RuntimeEquipedData GetEquipedFromSlotID(string slot);
	}
}
