using System;

namespace SpaxUtils
{
	/// <summary>
	/// Service that manages and keeps track of world cycles and world activation state.
	/// </summary>
	public class WorldService : IService
	{
		public event Action<int> NewCycleEvent;

		/// <summary>
		/// Invoked when the world is activated/deactivated.
		/// Parameter:
		/// - bool active: True when world is active (agents may activate), false when world is inactive (agents should deactivate).
		/// </summary>
		public event Action<bool> WorldActiveChangedEvent;

		/// <summary>
		/// The current cycle count for the active profile.
		/// </summary>
		public int Cycle { get; private set; }

		/// <summary>
		/// Whether the world is currently active.
		/// </summary>
		public bool WorldActive { get; private set; } = true; // Keep backward compatibility: existing spawners used to spawn on Start.

		private RuntimeDataService runtimeDataService;
		private GameService gameService;

		public WorldService(RuntimeDataService runtimeDataService, GameService gameService)
		{
			this.runtimeDataService = runtimeDataService;
			this.gameService = gameService;

			runtimeDataService.CurrentProfileChangedEvent += OnCurrentDataProfileChangedEvent;
			OnCurrentDataProfileChangedEvent(runtimeDataService.CurrentProfile);

			// Deactivate the world only once the loading screen is fully blocking the view.
			gameService.LoadingScreenShownEvent += OnLoadingScreenShownEvent;

			// Activate the world when entering GAME (still behind black, before fade-out).
			// Also increment cycle on Respawn.
			gameService.EnteredStateEvent += OnEnteredStateEvent;
		}

		/// <summary>
		/// Activate the world (agents may spawn/activate).
		/// </summary>
		public void Activate()
		{
			SetWorldActive(true);
		}

		/// <summary>
		/// Deactivate the world (agents should deactivate).
		/// </summary>
		public void Deactivate()
		{
			SetWorldActive(false);
		}

		/// <summary>
		/// Activate or deactivate the world, deciding whether agents should be active or inactive.
		/// </summary>
		public void SetWorldActive(bool active)
		{
			if (WorldActive == active)
			{
				return;
			}

			WorldActive = active;
			WorldActiveChangedEvent?.Invoke(active);
		}

		/// <summary>
		/// Have the currently loaded profile enter a new game cycle.
		/// </summary>
		public void NewCycle()
		{
			Cycle++;
			runtimeDataService.CurrentProfile.SetValue(ProfileDataIdentifiers.CYCLE, Cycle);
			NewCycleEvent?.Invoke(Cycle);
		}

		private void OnCurrentDataProfileChangedEvent(RuntimeDataCollection profile)
		{
			if (profile != null)
			{
				Cycle = profile.GetValue<int>(ProfileDataIdentifiers.CYCLE, -1);
			}
			else
			{
				Cycle = -1;
			}
		}

		private void OnLoadingScreenShownEvent(string targetState, string targetScene, GameStateSwitchReason reason)
		{
			// Only deactivate once the view is blocked (black), so no pop-in/out is visible.
			if (targetState != GameStateIdentifiers.GAME)
			{
				Deactivate();
			}
		}

		private void OnEnteredStateEvent(string enteredState, string activeScene, GameStateSwitchReason reason)
		{
			if (enteredState != GameStateIdentifiers.GAME)
			{
				return;
			}

			// On respawn we always start a new cycle. No state checks needed.
			if (reason == GameStateSwitchReason.Respawn)
			{
				NewCycle();
			}

			// World becomes active once GAME is entered (still behind black, before fade-out).
			Activate();
		}
	}
}
