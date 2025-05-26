using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public abstract class Vector8ConfigurationAssetBase : ScriptableObject, IDependencyFactory
	{
		protected abstract string Key { get; }
		protected abstract string Interpolator { get; }

		[SerializeField] private bool ranged;
		[SerializeField, Conditional(nameof(ranged), false)] private bool random;
		[SerializeField, Conditional(nameof(ranged), true)] private RangedOctad fixedValues;
		[SerializeField, Conditional(nameof(ranged), false)] private MinMaxOctad rangedValues;

		public void Bind(IDependencyManager dependencyManager)
		{
			if (ranged)
			{
				RuntimeDataCollection data = dependencyManager.Get<RuntimeDataCollection>(true, false);
				if (random)
				{
					// Randomize the configuration by retrieving the seed from runtime data.
					if (data != null && data.TryGetValue(EntityDataIdentifiers.SEED, out int seed))
					{
						dependencyManager.Bind(Key, rangedValues.Randomize(seed));
					}
					else
					{
						SpaxDebug.Error("No seed data found.", $"Tried to randomize a {nameof(Vector8ConfigurationAssetBase)} but no seed data was found. A random seed will be used instead.");
						dependencyManager.Bind(Key, rangedValues.Randomize(Random.Range(int.MinValue, int.MaxValue)));
					}
				}
				else
				{
					// Interpolate configuration data by retrieving interpolator from runtime data.
					if (data != null && data.TryGetValue(Interpolator, out float t))
					{
						dependencyManager.Bind(Key, rangedValues.Interpolate(t));
					}
					else
					{
						if (data != null)
						{
							SpaxDebug.Error("No interpolator data found.", $"For key: \"{Interpolator}\" in collection: \"{data.ID}\".\nTried to interpolate a {nameof(Vector8ConfigurationAssetBase)} but no interpolator data was found. A value 0.5 will be used instead.\nData:\n{data}");
						}
						else
						{
							SpaxDebug.Error("No data found for interpolator.", $"For key: \"{Interpolator}\".\nTried to interpolate a {nameof(Vector8ConfigurationAssetBase)} but no interpolator data was found. A value 0.5 will be used instead.");
						}
						dependencyManager.Bind(Key, rangedValues.Interpolate(0.5f));
					}
				}
			}
			else
			{
				dependencyManager.Bind(Key, fixedValues.Vector8);
			}
		}
	}
}
