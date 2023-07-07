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
			LoadConfigurations();
		}

		private void LoadConfigurations()
		{
			sheets = Resources.LoadAll<StatConfigurationSheet>(PATH);
		}

		/// <inheritdoc/>
		public bool TryGet(string identifier, out IStatConfiguration configuration)
		{
			// Check if configuration was cached already.
			if (cache.ContainsKey(identifier))
			{
				configuration = cache[identifier];
				return true;
			}

			configuration = null;

			// Prevent circular parent dependencies.
			if (currentlyResolving.Contains(identifier))
			{
				SpaxDebug.Error("Circular stat parenting conflict.",
					$"Current: {identifier}, In progress: [{string.Join(", ", currentlyResolving)}]");
				return false;
			}
			else
			{
				currentlyResolving.Add(identifier);
			}

			// Search for stat configuration in all registered configuration sheets.
			foreach (StatConfigurationSheet sheet in sheets)
			{
				if (sheet.TryGet(identifier, out configuration))
				{
					if (configuration.CopyParent)
					{
						// Create a new config containing parent data.
						if (TryGet(configuration.ParentIdentifier, out IStatConfiguration parent))
						{
							var config = new StatConfigStruct(configuration, parent);
							configuration = config;
						}
						else
						{
							SpaxDebug.Error("Could not find parent stat config. Returning parent-less config.",
								$"\"{configuration.Identifier}\" requires parent; \"{configuration.ParentIdentifier}\"");
						}
					}

					currentlyResolving.Remove(identifier);
					cache.Add(identifier, configuration);
					return true;
				}
			}

			currentlyResolving.Remove(identifier);
			return false;
		}

		/// <inheritdoc/>
		public IStatConfiguration Get(string identifier)
		{
			TryGet(identifier, out IStatConfiguration configuration);
			return configuration;
		}
	}
}
