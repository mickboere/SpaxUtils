using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class Loot
	{
		public ItemDataAsset Asset => asset;
		public float Odds => odds;
		public LabeledDataCollection Data => data;

		[SerializeField] private ItemDataAsset asset;
		[SerializeField, Range(0f, 1f)] private float odds = 1f;
		[SerializeField] private LabeledDataCollection data;
		// TODO: Options for randomized item data (quantity, stats, etc.)

		public RuntimeItemData ToRuntimeItemData()
		{
			var d = new RuntimeItemData(asset, data.ToRuntimeDataCollection(), null);
			// TODO: Implement randomized item data here.
			return d;
		}
	}
}
