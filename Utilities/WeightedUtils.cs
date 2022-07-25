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
			float sum = weightedList.Sum((e) => e.Weight) * value;
			for (int i = 0; i < weightedList.Count; i++)
			{
				sum -= weightedList[i].Weight;
				if (sum <= 0f)
				{
					progress = 1f - Mathf.Abs(sum) / weightedList[i].Weight;
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
			return Index(weightedList, Random.value, out _);
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
	}
}
