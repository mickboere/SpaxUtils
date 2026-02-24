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
			for (int i = 0; i < map.StatMappings.Count; i++)
			{
				StatMapping mapping = map.StatMappings[i];

				// Get the target stat to add the mapping to.
				EntityStat toStat = Entity.Stats.GetStat(mapping.ToStat, true);

				string id = map.name + "_" + i.ToString();
				if (!toStat.HasModifier(id))
				{
					// Get the stat we're going to use as input value for the mapping.
					EntityStat fromStat = Entity.Stats.GetStat(mapping.FromStat, true);

					// Create the mapping modifier.
					StatModifier mod = new StatModifier(mapping, fromStat, mapping.SourceBase);

					// Add the mapping modifier.
					toStat.AddModifier(id, mod);
				}
				else
				{
					SpaxDebug.Error($"Stat '{mapping.ToStat}' somehow already contains a mapping for id '{id}' from '{mapping.FromStat}'.", "Mapping was not added.", map);
				}
			}
		}
	}
}
