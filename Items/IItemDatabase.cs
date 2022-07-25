namespace SpaxUtils
{
	/// <summary>
	/// Interface for item databases.
	/// </summary>
	public interface IItemDatabase
	{
		void AddItem(IItemData itemData);
		IItemData GetItem(string id);
		bool HasItem(string id);
		string GetUniqueID(string method);
	}
}
