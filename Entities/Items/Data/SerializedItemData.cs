using UnityEngine;
using System;

namespace SpaxUtils
{
	[Serializable]
	public class SerializedItemData
	{
		public ItemDataAsset Asset => asset;
		public LabeledDataCollection Data => data;

		[SerializeField, Expandable] private ItemDataAsset asset;
		[SerializeField] private LabeledDataCollection data;

		public RuntimeItemData ToRuntimeItemData()
		{
			return new RuntimeItemData(asset, data.ToRuntimeDataCollection(), null);
		}
	}
}
