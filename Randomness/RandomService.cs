using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service managing game randomness.
	/// </summary>
	public class RandomService : IService
	{
		/// <summary>
		/// The seed which roots all other randomness, generated from the current loaded profile name.
		/// </summary>
		public int GameSeed { get; private set; }

		private RuntimeDataService runtimeDataService;

		public RandomService(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;
			runtimeDataService.CurrentProfileChangedEvent += OnCurrentDataProfileChangedEvent;
			OnCurrentDataProfileChangedEvent(runtimeDataService.CurrentProfile);
		}

		private void OnCurrentDataProfileChangedEvent(RuntimeDataCollection profile)
		{
			GameSeed = runtimeDataService.CurrentProfile.GetValue<int>(ProfileDataIdentifiers.SEED);
		}

		/// <summary>
		/// Generates a deterministic seed from the loaded profile's game seed and the provided string <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The string/id to generate the local seed for.</param>
		/// <returns>A deterministic seed generated from the loaded profile's game seed and the provided string <paramref name="id"/>.</returns>
		public int GenerateSeed(string id)
		{
			return GameSeed.Combine(id.GetDeterministicHashCode());
		}

		/// <summary>
		/// Generates a completely non-reproducable seed using <see cref="Random.Range(int, int)"/>.
		/// </summary>
		public static int GenerateSeed()
		{
			return Random.Range(int.MinValue, int.MaxValue);
		}
	}
}
