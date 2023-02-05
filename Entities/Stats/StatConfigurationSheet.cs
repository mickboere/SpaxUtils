using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "StatConfigurationSheet", menuName = "ScriptableObjects/Stats/Configuration Sheet")]
	public class StatConfigurationSheet : ScriptableObject, IStatLibrary
	{
		[SerializeField] private List<StatConfiguration> configurations;

		private Dictionary<string, StatConfiguration> dictionary;

		public bool TryGet(string identifier, out IStatConfiguration configuration)
		{
			if (dictionary == null || dictionary.Count != configurations.Count)
			{
				dictionary = new Dictionary<string, StatConfiguration>();
				foreach (StatConfiguration c in configurations)
				{
					dictionary[c.Identifier] = c;
				}
			}

			if (dictionary.ContainsKey(identifier))
			{
				configuration = dictionary[identifier];
				return true;
			}

			configuration = null;
			return false;
		}

		public IStatConfiguration Get(string identifier)
		{
			TryGet(identifier, out IStatConfiguration configuration);
			return configuration;
		}
	}
}
