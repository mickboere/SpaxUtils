namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEquipmentSlotTypeConstants"/> implementation for SpiritAxis.
	/// </summary>
	public class EquipmentSlotTypes : IEquipmentSlotTypeConstants
	{
		public const string RIGHT_HAND = "Right Hand"; // 1 spot (right hand)
		public const string LEFT_HAND = "Left Hand"; // 1 spot (left hand)
		public const string RING = "Ring"; // 8 unique spots (all fingers but thumbs)
		public const string APPAREL = "Apparel"; // n spots (infinite as long as the locations don't overlap).
	}
}
