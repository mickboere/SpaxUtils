using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(LootTable), menuName = "ScriptableObjects/" + nameof(LootTable))]
	public class LootTable : ScriptableObject
	{
		[Serializable]
		public class Loot
		{
			public float Odds => odds;
			public SerializedItemData Item => item;

			[SerializeField, Range(0f, 1f)] private float odds = 1f;
			[SerializeField] private SerializedItemData item;
			// TODO: Options for randomized item data (quantity, stats, etc.)
		}

		[SerializeField] private List<Loot> loot;
		[SerializeField] private float overallOdds = 1f;

		public List<SerializedItemData> GenerateLoot(int seed, float oddsMultiplier = 1f)
		{
			List<SerializedItemData> result = new List<SerializedItemData>();
			foreach (Loot l in loot)
			{
				int s = seed.Combine(l.Item.Asset.ID.GetDeterministicHashCode());
				UnityEngine.Random.InitState(s);
				if (UnityEngine.Random.value <= overallOdds * l.Odds * oddsMultiplier)
				{
					result.Add(l.Item);
				}
			}
			return result;
		}
	}
}
