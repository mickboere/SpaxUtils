using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that manages and keeps track of game cycles.
	/// </summary>
	public class CycleService : IService
	{
		public event Action<int> NewCycleEvent;

		/// <summary>
		/// The current cycle count for the active profile.
		/// </summary>
		public int Count { get; private set; }

		private RuntimeDataService runtimeDataService;

		public CycleService(RuntimeDataService runtimeDataService)
		{
			this.runtimeDataService = runtimeDataService;
			runtimeDataService.CurrentProfileChangedEvent += OnCurrentDataProfileChangedEvent;
			OnCurrentDataProfileChangedEvent(runtimeDataService.CurrentProfile);
		}

		/// <summary>
		/// Have the currently loaded profile enter a new game cycle.
		/// </summary>
		public void NewCycle()
		{
			Count++;
			runtimeDataService.CurrentProfile.SetValue(ProfileDataIdentifiers.CYCLE, Count);
			NewCycleEvent?.Invoke(Count);
		}

		private void OnCurrentDataProfileChangedEvent(RuntimeDataCollection profile)
		{
			Count = runtimeDataService.CurrentProfile.GetValue<int>(ProfileDataIdentifiers.CYCLE, -1);
		}
	}
}
