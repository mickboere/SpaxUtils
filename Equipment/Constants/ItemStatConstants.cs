namespace SpaxUtils
{
	/// <summary>
	/// Stat data identifiers for items.
	/// </summary>
	public class ItemStatConstants : ILabeledDataIdentifierConstants
	{
		private const string ITEMS = "ITEMS/";

		// Equipment stats
		private const string EQUIPMENT = ITEMS + "EQUIPMENT/";
		public const string MASS = EQUIPMENT + "Mass";
		public const string HARDNESS = EQUIPMENT + "Hardness";
		public const string SHARPNESS = EQUIPMENT + "Sharpness";
	}
}
