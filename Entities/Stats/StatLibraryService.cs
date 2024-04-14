using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IStatLibrary"/> service containing references to all <see cref="StatConfiguration"/> resources at <see cref="PATH"/>.
	/// </summary>
	public class StatLibraryService : IStatLibrary, IService
	{
		public const string PATH = "Stats/Configurations";

		private StatConfigurationSheet[] sheets;

		private List<string> currentlyResolving = new List<string>();
		private Dictionary<string, IStatConfiguration> cache = new Dictionary<string, IStatConfiguration>();

		public StatLibraryService()
		{
			Initialize();
		}

		private void Initialize()
		{
			// Load all configurations from resources into one giant dictionary.
			sheets = Resources.LoadAll<StatConfigurationSheet>(PATH);
			Dictionary<string, StatConfiguration> masterList = new Dictionary<string, StatConfiguration>();
			foreach (StatConfigurationSheet sheet in sheets)
			{
				foreach (StatConfiguration config in sheet.Configurations)
				{
					if (masterList.ContainsKey(config.Identifier))
					{
						SpaxDebug.Error($"Duplicate stat configurations of {config.Identifier}", $"duplicate found in {sheet.name}", sheet);
					}
					else
					{
						masterList.Add(config.Identifier, config);
					}
				}
			}

			// Initialize all individual stat configurations.
			foreach (string identifier in masterList.Keys)
			{
				Resolve(identifier);
			}

			IStatConfiguration Resolve(string identifier)
			{
				// If resolved already, return cached result.
				if (cache.ContainsKey(identifier))
				{
					return cache[identifier];
				}

				// Check if we can resolve at all (we always can unless called recursively).
				if (!masterList.ContainsKey(identifier))
				{
					return null;
				}

				// Prevent circular parent dependencies.
				if (currentlyResolving.Contains(identifier))
				{
					SpaxDebug.Error("Circular stat parenting conflict.",
						$"Current: {identifier}, In progress: [{string.Join(", ", currentlyResolving)}]");
					return null;
				}

				// Begin resolving:
				currentlyResolving.Add(identifier);
				IStatConfiguration result = masterList[identifier];

				// Recursively resolve parent(s).
				if (result.CopyParent)
				{
					IStatConfiguration parent = Resolve(result.ParentIdentifier);

					if (parent == null)
					{
						SpaxDebug.Error("Could not find parent stat config. Creating parent-less config.",
							$"\"{result.Identifier}\" requires parent; \"{result.ParentIdentifier}\"");
					}
					else
					{
						var parentedStatConfig = new ParentedStatConfig(result, parent);
						result = parentedStatConfig;
					}
				}

				// Resolve sub-stats.
				if (result.HasSubStats)
				{
					List<ISubStatConfiguration> subStats = result.SubStats;
					foreach (ISubStatConfiguration subStat in subStats)
					{
						ParentedSubStatConfig parentedSubStatConfig = new ParentedSubStatConfig(subStat, result);
						cache.Add(parentedSubStatConfig.Identifier, parentedSubStatConfig);
					}
				}

				currentlyResolving.Remove(identifier);
				cache.Add(identifier, result);
				return result;
			}
		}

		/// <inheritdoc/>
		public bool TryGet(string identifier, out IStatConfiguration configuration)
		{
			if (cache.ContainsKey(identifier))
			{
				configuration = cache[identifier];
				return true;
			}

			configuration = null;
			return false;
		}

		/// <inheritdoc/>
		public IStatConfiguration Get(string identifier)
		{
			if (!TryGet(identifier, out IStatConfiguration configuration))
			{
				SpaxDebug.Warning("Could not find configuration for stat:", identifier);
			}

			return configuration;
		}
	}
}
