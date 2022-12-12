using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class that manages any amount of stats, a stat being a [string, <see cref="CompositeFloatBase"/>] KeyValuePair.
	/// </summary>
	[Serializable]
	public class StatCollection<T> where T : CompositeFloatBase
	{
		public event Action<T> AddedStatEvent;

		public IReadOnlyDictionary<string, T> Dictionary => stats;
		public IEnumerable<T> Stats => stats.Values;

		public int Count => stats.Count;

		[SerializeField] private Dictionary<string, T> stats;

		public StatCollection()
		{
			stats = new Dictionary<string, T>();
		}

		public bool TryAddStat(string stat, T value)
		{
			if (HasStat(stat))
			{
				return false;
			}
			else
			{
				AddStat(stat, value);
				return true;
			}
		}

		public void AddStat(string stat, T value)
		{
			if (HasStat(stat))
			{
				SpaxDebug.Warning("StatCollection: ", $"Adding a stat but one already exists with the same key ({stat}), overwriting...");
			}

			stats.Add(stat, value);
			AddedStatEvent?.Invoke(value);
		}

		public bool HasStat(string stat)
		{
			return stats.ContainsKey(stat);
		}

		public T GetStat(string stat)
		{
			return stats[stat];
		}

		public bool TryGetStat(string stat, out T value)
		{
			value = null;
			if (HasStat(stat))
			{
				value = GetStat(stat);
				return true;
			}
			return false;
		}

		public void Modify(IStatModifier modifier)
		{
			if (TryGetStat(modifier.Stat, out T stat))
			{
				stat.AddModifier(modifier.Identifier, modifier.Modifier);
			}
		}
	}
}