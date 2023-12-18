namespace SpaxUtils
{
	/// <summary>
	/// Stat data identifiers for items.
	/// </summary>
	public class ItemStatConstants : ILabeledDataIdentifierConstants
	{
		private const string ITEMS = "ITEMS/";

		// < Equipment >
		private const string EQUIPMENT = ITEMS + "EQUIPMENT/";

		public const string MASS = EQUIPMENT + "Mass"; // Decides impact force and required strength.
		public const string DEFENCE = EQUIPMENT + "Defence"; // Defensive power added to Body/Defence.

		public const string OFFENCE = EQUIPMENT + "Offence"; // Raw offensive power added to Body/Offence.
	}
}
