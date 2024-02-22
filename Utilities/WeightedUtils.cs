using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public static class WeightedUtils
	{
		/// <summary>
		/// Samples <paramref name="weightedList"/> at normalized progress <paramref name="value"/>.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IWeightedElement"/> to sample.</typeparam>
		/// <param name="weightedList">The collection of <see cref="IWeightedElement"/>s.</param>
		/// <param name="value">The normalized progress value.</param>
		/// <param name="progress">The normalized progress into the sampled index.</param>
		/// <returns>The index of the resulting item.</returns>
		public static int Index<T>(IList<T> weightedList, float value, out float progress) where T : IWeightedElement
		{
			float sum = weightedList.Sum((e) => e.ElementWeight) * value;
			for (int i = 0; i < weightedList.Count; i++)
			{
				sum -= weightedList[i].ElementWeight;
				if (sum <= 0f)
				{
					progress = 1f - Mathf.Abs(sum) / weightedList[i].ElementWeight;
					return i;
				}
			}

			progress = 1f;
			return weightedList.Count - 1;
		}

		/// <summary>
		/// Samples <paramref name="weightedList"/> at normalized progress <paramref name="value"/>.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IWeightedElement"/> to sample.</typeparam>
		/// <param name="weightedList">The collection of <see cref="IWeightedElement"/>s.</param>
		/// <param name="value">The normalized progress value.</param>
		/// <param name="progress">The normalized progress into the sampled item.</param>
		/// <returns>The resulting item.</returns>
		public static T Item<T>(IList<T> weightedList, float value, out float progress) where T : IWeightedElement
		{
			return weightedList[Index(weightedList, value, out progress)];
		}

		/// <summary>
		/// Retrieves a random index based on Weighted Randomness.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IWeightedElement"/> to sample.</typeparam>
		/// <param name="weightedList">The collection of <see cref="IWeightedElement"/>s.</param>
		/// <returns>The random index.</returns>
		public static int RandomIndex<T>(IList<T> weightedList) where T : IWeightedElement
		{
			return Index(weightedList, UnityEngine.Random.value, out _);
		}

		/// <summary>
		/// Retrieves a random item based on Weighted Randomness.
		/// </summary>
		/// <typeparam name="T">The type of <see cref="IWeightedElement"/> to sample.</typeparam>
		/// <param name="weightedList">The collection of <see cref="IWeightedElement"/>s.</param>
		/// <returns>The random item.</returns>
		public static T RandomItem<T>(IList<T> weightedList) where T : IWeightedElement
		{
			return weightedList[RandomIndex(weightedList)];
		}

		/// <summary>
		/// Retrieve priotized normalized weights from a weighted list of items with unique priorities.
		/// Highest priority has room to utilize full 0 to 1 weight, lower priorities get only what's left over.
		/// </summary>
		/// <typeparam name="T">The item type to iterate over.</typeparam>
		/// <param name="items">The list of items to iterate over.</param>
		/// <param name="priorityGetter">Func that, when given an item from the <paramref name="items"/> list, should return its priority.</param>
		/// <param name="weightGetter">Func that, when given an item from the <paramref name="items"/> list, should return its weight (0 - 1).</param>
		/// <returns>A dictionary with the prioritized normalized weights.</returns>
		public static Dictionary<T, float> GetPrioritizedNormalizedWeights<T>(IEnumerable<T> items, Func<T, int> priorityGetter, Func<T, float> weightGetter, bool normalizeResult = false)
		{
			Dictionary<T, float> effectiveWeights = new Dictionary<T, float>();

			// TODO: Optimize by caching priorityGetter() and weightGetter() results;

			if (items == null || items.Count() == 0)
			{
				return effectiveWeights;
			}

			List<T> orderedItems = items.OrderByDescending((e) => weightGetter(e)).OrderByDescending((e) => priorityGetter(e)).ToList();
			int priority = priorityGetter(orderedItems[0]) + 1;
			float sum = 0f;
			float totalWeight = 0f;
			float range = 1f;

			for (int i = 0; i < orderedItems.Count; i++)
			{
				T current = orderedItems[i];
				int prio = priorityGetter(current);
				if (prio < priority)
				{
					// Shift current priority level.
					priority = prio;
					sum = Mathf.Max(1f, orderedItems.Sum((e) => priorityGetter(e) == priority ? weightGetter(e) : null).Value);
					range = 1f - totalWeight;
				}

				float weight = weightGetter(current) / sum * range;
				totalWeight += weight;
				effectiveWeights[current] = weight;

				if (totalWeight.Approx(1f) || totalWeight > 1f)
				{
					// Reached max weight, break loop.
					break;
				}
			}

			if (normalizeResult)
			{
				// Normalize weights.
				sum = effectiveWeights.Values.Sum();
				foreach (KeyValuePair<T, float> item in effectiveWeights)
				{
					effectiveWeights[item.Key] = item.Value / sum;
				}
			}

			return effectiveWeights;
		}
	}
}
