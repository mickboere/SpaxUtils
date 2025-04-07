using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = nameof(LootTable), menuName = "ScriptableObjects/" + nameof(LootTable))]
	public class LootTable : ScriptableObject
	{
		[SerializeField] private List<Loot> loot;
		[SerializeField] private float overallOdds = 1f;

		public List<RuntimeItemData> GenerateLoot(int seed, float oddsMultiplier = 1f)
		{
			List<RuntimeItemData> result = new List<RuntimeItemData>();
			foreach (Loot l in loot)
			{
				if (l.Odds.Approx(1f))
				{
					result.Add(l.ToRuntimeItemData());
					continue;
				}

				int s = seed.Combine(l.Asset.ID.GetDeterministicHashCode());
				Random.InitState(s);
				if (Random.value <= overallOdds * l.Odds * oddsMultiplier)
				{
					result.Add(l.ToRuntimeItemData());
				}
			}

			return result;
		}
	}
}
