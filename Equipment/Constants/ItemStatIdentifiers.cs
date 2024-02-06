namespace SpaxUtils
{
	/// <summary>
	/// Stat identifiers for item data.
	/// </summary>
	public class ItemStatIdentifiers : ILabeledDataIdentifiers
	{
		// < Item stats >
		//private const string ITEMS = IStatIdentifierConstants.STATS + "ITEMS/";

		// < Equipment stats >
		private const string EQUIPMENT = IStatIdentifierConstants.STATS + "EQUIPMENT/";

		public const string MASS = EQUIPMENT + "Mass"; // Mass of the equipment.
		public const string DEFENCE = EQUIPMENT + "Defence"; // Defensive power of the equipment.
		public const string GUARD = EQUIPMENT + "Guard"; // Guarding power of the equipment.
		public const string OFFENCE = EQUIPMENT + "Offence"; // Offensive power of the equipment.
		public const string PIERCING = EQUIPMENT + "Piercing"; // Piercing of the equipment (0-1). 0 will not pierce, 1 will pierce anything.
	}
}
