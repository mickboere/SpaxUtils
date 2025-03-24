namespace SpaxUtils
{
	public class InteractionTypes : IInteractionTypes
	{
		public const string ITEM = "Item";
		public const string ITEM_TAKE = ITEM + "/Take";
		public const string ITEM_EQUIP = ITEM + "/Equip";

		public const string INVENTORY = "Inventory";
		public const string INVENTORY_GIVE = INVENTORY + "/Give";

		public const string CONTAINER = "Container";
		public const string CONTAINER_OPEN = CONTAINER + "/Open";

		public const string DOOR = "Door";
		public const string DOOR_OPEN = DOOR + "/Open";
		public const string DOOR_CLOSE = DOOR + "/Close";
	}
}
