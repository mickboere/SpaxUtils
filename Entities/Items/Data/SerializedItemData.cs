using UnityEngine;
using System;

namespace SpaxUtils
{
	[Serializable]
	public class SerializedItemData
	{
		public ItemDataAsset Asset => asset;
		public LabeledDataCollection Data => data;

		[SerializeField] private ItemDataAsset asset;
		[SerializeField] private LabeledDataCollection data;

		public RuntimeItemData ToRuntimeItemData()
		{
			return new RuntimeItemData(asset, data.ToRuntimeDataCollection(), null);
		}
	}
}
