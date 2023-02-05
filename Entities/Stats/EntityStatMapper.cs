using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that takes a <see cref="StatMappingSheet"/> to modify one stat with another.
	/// Stats that do not exist yet will be created.
	/// </summary>
	public class EntityStatMapper : EntityComponentBase
	{
		[SerializeField] private StatMappingSheet[] mappingSheets;

		public override void InjectDependencies(IEntity entity)
		{
			base.InjectDependencies(entity);

			AddStatMappings();
		}

		private void AddStatMappings()
		{
			if (mappingSheets.Length == 0)
			{
				return;
			}

			foreach (StatMappingSheet statMappingSheet in mappingSheets)
			{
				// Configure the defined stat-to-stat mappings.
				foreach (StatMapping mapping in statMappingSheet.Mappings)
				{
					// Get the target stat to add the mapping to.
					EntityStat toStat = Entity.GetStat(mapping.ToStat, true);

					// Only add the modifier if this stat does not have a mapping from the mapping stat yet.
					// Easily checkable since we use the input stat's identifier as mod identifier.
					if (!toStat.HasModifier(mapping.FromStat))
					{
						// Get the stat we're going to use as input value for the mapping.
						EntityStat fromStat = Entity.GetStat(mapping.FromStat, true);

						// Create the mapping modifier.
						StatMappingModifier mod = new StatMappingModifier(mapping, fromStat);

						// Add the mapping modifier.
						toStat.AddModifier(mapping.FromStat, mod);
					}
					else
					{
						SpaxDebug.Error($"Stat '{mapping.ToStat}' already contains a mapping from '{mapping.FromStat}'.", "Mapping was not added.");
					}
				}
			}
		}
	}
}
