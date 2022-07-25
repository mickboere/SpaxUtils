using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils;
using System;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of <see cref="StatSetting"/>s.
	/// </summary>
	[CreateAssetMenu(fileName = "StatLibrary", menuName = "ScriptableObjects/Stat Library")]
	public class StatLibrary : ScriptableObject, IStatLibrary
	{
		public IReadOnlyList<IStatSetting> Settings => settings;

		[SerializeField] private List<StatSetting> settings;

#if UNITY_EDITOR
		public void RegenerateStats()
		{
			settings.Clear();
			GenerateStats();
		}

		public void GenerateStats()
		{
			// TODO: Remove spirit axis dependency.
			List<string> allStats = typeof(SpiritAxis.EntityStatIdentifiers).GetAllPublicConstStrings(false);
			List<string> currentStats = settings.Select((setting) => setting.Identifier).ToList();
			List<string> missingStats = allStats.Where((stat) => !currentStats.Contains(stat)).ToList();

			foreach (string stat in missingStats)
			{
				string[] splitName = stat.Split('/');
				string name = splitName[splitName.Length - 1];
				settings.Add(new StatSetting(stat, name, "", 0f, Color.white, null));
			}

			settings = settings.OrderBy((setting) => allStats.IndexOf(setting.Identifier)).ToList();
		}
#endif

		public IStatSetting Get(string identifier)
		{
			return settings.FirstOrDefault((s) => s.Identifier.Equals(identifier));
		}
	}
}
