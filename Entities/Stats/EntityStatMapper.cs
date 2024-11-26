using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that takes a <see cref="StatMap"/> to modify one stat with another.
	/// Stats that do not exist yet will be created.
	/// </summary>
	[DefaultExecutionOrder(-999)]
	public class EntityStatMapper : EntityComponentMono
	{
		[SerializeField] private StatAtlas[] atlases;
		[SerializeField] private StatMap[] maps;

		public override void InjectDependencies(IEntity entity)
		{
			base.InjectDependencies(entity);

			AddStatMappings();
		}

		private void AddStatMappings()
		{
			foreach (StatAtlas atlas in atlases)
			{
				foreach (StatMap map in atlas.Maps)
				{
					AddMap(map);
				}
			}

			foreach (StatMap map in maps)
			{
				AddMap(map);
			}
		}

		private void AddMap(StatMap map)
		{
			// Configure the defined stat-to-stat mappings.
			foreach (StatMapping mapping in map.Mappings)
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
					StatModifier mod = new StatModifier(mapping, fromStat);

					// Add the mapping modifier.
					toStat.AddModifier(mapping.FromStat, mod);
				}
				else
				{
					SpaxDebug.Error($"Stat '{mapping.ToStat}' already contains a mapping from '{mapping.FromStat}'.", "Mapping was not added.", map);
				}
			}
		}
	}
}
