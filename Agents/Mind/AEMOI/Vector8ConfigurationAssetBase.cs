using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public abstract class Vector8ConfigurationAssetBase : ScriptableObject, IDependencyFactory
	{
		protected abstract string Key { get; }

		[SerializeField] private bool random;
		[SerializeField, Conditional(nameof(random), true)] private RangedOctad fixedValues;
		[SerializeField, Conditional(nameof(random), false)] private MinMaxOctad randomValues;

		public void Bind(IDependencyManager dependencyManager)
		{
			if (random)
			{
				RuntimeDataCollection data = dependencyManager.Get<RuntimeDataCollection>(true, false);
				if (data != null && data.TryGetValue(EntityDataIdentifiers.SEED, out int seed))
				{
					dependencyManager.Bind(Key, randomValues.Randomize(seed));
				}
				else
				{
					SpaxDebug.Error("No seed data found.", $"Tried to randomize a {nameof(Vector8ConfigurationAssetBase)} but no seed data was found.");
					dependencyManager.Bind(Key, randomValues.Randomize(RandomService.GenerateSeed()));
				}
			}
			else
			{
				dependencyManager.Bind(Key, fixedValues.Vector8);
			}
		}
	}
}
