namespace SpaxUtils
{
	/// <summary>
	/// How a piece of equipment is carried when it isn't in hand. Authored per item;
	/// the body plan decides where each category actually goes.
	/// </summary>
	public class SheatheCategories : ISheatheCategoryConstants
	{
		public const string SMALL_ARMS = "Small Arms"; // Hip, opposite the arm that draws it.
		public const string LARGE_ARMS = "Large Arms"; // Back, angled toward the arm that draws it.
		public const string SMALL_ITEM = "Small Item"; // Belt quick-slot.
		public const string LARGE_ITEM = "Large Item"; // Back, outside the weapons.
	}
}
