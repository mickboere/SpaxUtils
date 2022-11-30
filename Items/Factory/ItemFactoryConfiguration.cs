using UnityEngine;

namespace SpaxUtils
{
	public class ItemFactoryConfiguration : ScriptableObject
	{
		public ItemComponent DefaultItemPrefab => defaultItemPrefab;

		[SerializeField] private ItemComponent defaultItemPrefab;
	}
}
