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
			foreach (StatConfigurationSheet sheet in sheets)
			{
				if (sheet.TryGet(identifier, out configuration))
				{
					return true;
				}
			}

			configuration = null;
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
