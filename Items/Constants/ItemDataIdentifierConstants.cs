namespace SpaxUtils
{
	public class ItemDataIdentifierConstants : ILabeledDataIdentifierConstants
	{
		#region Base Categories
		private const string STRING = ILabeledDataIdentifierConstants.STRING;
		private const string INT = ILabeledDataIdentifierConstants.INT;
		private const string FLOAT = ILabeledDataIdentifierConstants.FLOAT;
		private const string BOOL = ILabeledDataIdentifierConstants.BOOL;
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
