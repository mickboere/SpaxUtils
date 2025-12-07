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
		public const string MASS = FLOAT + "Mass"; // Maps to agent Load and Mass/limb.
		public const string REACH = FLOAT + "Reach"; // Maps to agent's Reach/limb.
		public const string SHIELD = FLOAT + "Shield"; // Maps explicitly to agent's Guard.
		public const string CAPACITY = FLOAT + "Capacity"; // Defines container capacity.
		public const string CONTAINS = FLOAT + "Contains"; // Defines current contained amount.

		// Bools
		public const string AETHERIAL = BOOL + "Aetherial";
		public const string UNIQUE = BOOL + "Unique";
		public const string CONSUME = BOOL + "Consume";
	}
}
