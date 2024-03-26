namespace SpaxUtils
{
	/// <summary>
	/// Interface for equipment slots, defining the type of slot and a unique ID used to reference this specific slot.
	/// </summary>
	public interface IEquipmentSlot : IIdentifiable
	{
		/// <summary>
		/// String defining the type of equipment to be allowed in this slot.
		/// </summary>
		string Type { get; }

		/// <summary>
		/// Takes care of optional reparenting and positioning of the equipment as well as any other slot-specific functionality.
		/// </summary>
		/// <param name="equipedData">The data to equip in this slot.</param>
		void Equip(RuntimeEquipedData equipedData);

		/// <summary>
		/// Unequips the currently equiped data, used for cleanup.
		/// </summary>
		void Unequip(RuntimeEquipedData equipedData);
	}
}
