using UnityEngine;

namespace SpaxUtils
{
	public abstract class Vector8ConfigurationAssetBase : ScriptableObject, IDependencyFactory
	{
		protected abstract string Key { get; }
		protected abstract string Interpolator { get; }

		[SerializeField] private bool ranged;
		[SerializeField, Conditional(nameof(ranged), true)] private RangedOctad fixedValues;
		[SerializeField, Conditional(nameof(ranged), false)] private MinMaxOctad rangedValues;
		[SerializeField, Range(0f, 1f)] private float randomness;

		public void Bind(IDependencyManager dependencyManager)
		{
			RuntimeDataCollection data = dependencyManager.Get<RuntimeDataCollection>(true, false);

			// ----- Seed -----
			int seed;
			if (data != null && data.TryGetValue(EntityDataIdentifiers.SEED, out int baseSeed))
			{
				// Offset per asset so multiple configs with same seed still differ.
				seed = baseSeed + GetHashCode();
			}
			else
			{
				SpaxDebug.Error(
					"No seed data found.",
					$"Tried to randomize a {nameof(Vector8ConfigurationAssetBase)} but no seed data was found. " +
					$"A random seed will be used instead."
				);
				seed = Random.Range(int.MinValue, int.MaxValue);
			}

			// ----- Deterministic base value -----
			Vector8 baseValue;

			if (ranged)
			{
				float t = 0.5f;

				if (data != null && data.TryGetValue(Interpolator, out float interp))
				{
					t = interp;
				}
				else
				{
					if (data != null)
					{
						SpaxDebug.Error(
							"No interpolator data found.",
							$"For key: \"{Interpolator}\" in collection: \"{data.ID}\".\n" +
							$"Tried to interpolate a {nameof(Vector8ConfigurationAssetBase)} but no interpolator data was found. " +
							$"A value 0.5 will be used instead.\nData:\n{data}"
						);
					}
					else
					{
						SpaxDebug.Error(
							"No data found for interpolator.",
							$"For key: \"{Interpolator}\".\n" +
							$"Tried to interpolate a {nameof(Vector8ConfigurationAssetBase)} but no interpolator data was found. " +
							$"A value 0.5 will be used instead."
						);
					}

					// Default mid-point if no interpolator present.
					t = 0.5f;
				}

				baseValue = rangedValues.Interpolate(t);
			}
			else
			{
				baseValue = fixedValues.Vector8; // already clamped 0–1
			}

			// ----- Deterministic random sample in same domain -----
			Vector8 randomValue = baseValue;

			if (randomness > 0f)
			{
				if (ranged)
				{
					// Random sample inside [min,max] per component (uses UnityEngine.Random internally).
					randomValue = rangedValues.Randomize(seed);
				}
				else
				{
					// Pure [0,1] per component (fixed config).
					randomValue = RandomUnitVector8(seed);
				}
			}

			// ----- Blend base vs random by slider -----
			Vector8 finalValue = randomness <= 0f
				? baseValue
				: Vector8.Lerp(baseValue, randomValue, randomness);

			dependencyManager.Bind(Key, finalValue);
		}

		private static Vector8 RandomUnitVector8(int seed)
		{
			Random.InitState(seed);
			return new Vector8(
				Random.value,
				Random.value,
				Random.value,
				Random.value,
				Random.value,
				Random.value,
				Random.value,
				Random.value);
		}
	}
}
