namespace SpaxUtils
{
	/// <summary>
	/// The sheathe points a body can offer. Fixed identifiers so a point can be addressed directly.
	/// ARM_SMALL_LEFT holds the LEFT arm's armament — which physically hangs on the right hip.
	/// </summary>
	public class SheathePointIdentifiers : ISheathePointIdentifiers
	{
		public const string ARM_SMALL_LEFT = "Sheathe Arm Small Left";
		public const string ARM_SMALL_RIGHT = "Sheathe Arm Small Right";
		public const string ARM_LARGE = "Sheathe Arm Large";

		public const string ITEM_LARGE = "Sheathe Item Large";

		// Belt quick-slots, indexed 0-2 front and 3-5 back. Reserved; not wired yet.
		public const string ITEM_0 = "Sheathe Item 0";
		public const string ITEM_1 = "Sheathe Item 1";
		public const string ITEM_2 = "Sheathe Item 2";
		public const string ITEM_3 = "Sheathe Item 3";
		public const string ITEM_4 = "Sheathe Item 4";
		public const string ITEM_5 = "Sheathe Item 5";
	}
}
