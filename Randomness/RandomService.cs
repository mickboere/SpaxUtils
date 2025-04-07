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
		/// Generates a deterministic seed by combining the active <see cref="GameSeed"/> with the <paramref name="subSeed"/>.
		/// </summary>
		public int GenerateSeed(int subSeed)
		{
			return GameSeed.Combine(subSeed);
		}

		/// <summary>
		/// Generates a deterministic seed by combining the active <see cref="GameSeed"/> with the provided string <paramref name="id"/>.
		/// </summary>
		public int GenerateSeed(string id)
		{
			return GenerateSeed(id.GetDeterministicHashCode());
		}

		/// <summary>
		/// Generates a deterministic seed by combining the active <see cref="GameSeed"/> with the provided parameters.
		/// </summary>
		public int GenerateSeed(string id, int subSeed)
		{
			return GenerateSeed(id.GetDeterministicHashCode().Combine(subSeed));
		}
	}
}
