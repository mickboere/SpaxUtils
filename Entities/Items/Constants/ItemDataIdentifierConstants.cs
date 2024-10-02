namespace SpaxUtils
{
	public class ItemDataIdentifierConstants : ILabeledDataIdentifiers
	{
		#region Base Categories
		private const string STRING = ILabeledDataIdentifiers.STRING;
		private const string INT = ILabeledDataIdentifiers.INT;
		private const string FLOAT = ILabeledDataIdentifiers.FLOAT;
		private const string BOOL = ILabeledDataIdentifiers.BOOL;
		#endregion

		// Strings
		public const string ITEM_ID = STRING + "ItemID";
		public const string NAME = STRING + "Name";
		public const string DESCRIPTION = STRING + "Description";
		public const string CATEGORY = STRING + "Category";

		// Ints
		public const string QUANTITY = INT + "Quantity";

		// Floats


		// Bools
		public const string UNIQUE = BOOL + "Unique";
	}
}
