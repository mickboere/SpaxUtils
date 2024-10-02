using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service responsible for creating world items.
	/// </summary>
	public class ItemFactoryService : IService
	{
		private ItemFactoryConfiguration config;

		public ItemFactoryService(ItemFactoryConfiguration config)
		{
			this.config = config;
		}

		public GameObject Create(IItemData itemData)
		{
			// TODO: implement
			return null;
		}
	}
}
