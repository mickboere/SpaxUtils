namespace SpaxUtils
{
	public class ItemDataIdentifiers : ILabeledDataIdentifiers
	{
		#region Base Categories
		private const string STRING = "ITEM/" + ILabeledDataIdentifiers.STRING;
		private const string INT = "ITEM/" + ILabeledDataIdentifiers.INT;
		private const string FLOAT = "ITEM/" + ILabeledDataIdentifiers.FLOAT;
		private const string BOOL = "ITEM/" + ILabeledDataIdentifiers.BOOL;
		#endregion

		// Strings
		public const string ITEM_ID = STRING + "ItemID";
		public const string NAME = STRING + "Name";

		// Ints
		public const string QUANTITY = INT + "Quantity";

		// Floats
		public const string VALUE = FLOAT + "Value";
		public const string CAPACITY = FLOAT + "Capacity";
		public const string CONTAINS = FLOAT + "Contains";

		// Bools
		public const string AETHERIAL = BOOL + "Aetherial";
		public const string UNIQUE = BOOL + "Unique";
		public const string CONSUME = BOOL + "Consume";
	}
}
