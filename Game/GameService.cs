using SpaxUtils.StateMachines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that manages the game's overall state.
	/// </summary>
	public class GameService : IService
	{
		public EventSystem EventSystem { get; }
		public Brain Brain { get; }

		/// <summary>
		/// Invoked when a switch request is accepted (before fade/load/transition work begins).
		/// Parameters:
		/// - string targetState: The desired game state ID (e.g. GAME, LOBBY).
		/// - string targetScene: The desired scene name (empty if no scene change).
		/// - GameStateSwitchReason reason: Why the switch was requested.
		/// </summary>
		public event Action<string, string, GameStateSwitchReason> SwitchRequestedEvent;

		/// <summary>
		/// Invoked when the loading screen is fully shown (i.e. view is blocked / black),
		/// right before scene load and/or state transition begins.
		/// Parameters:
		/// - string targetState: The desired game state ID (e.g. GAME, LOBBY).
		/// - string targetScene: The desired scene name (empty if no scene change).
		/// - GameStateSwitchReason reason: Why the switch was requested.
		/// </summary>
		public event Action<string, string, GameStateSwitchReason> LoadingScreenShownEvent;

		/// <summary>
		/// Invoked right after the state machine transitioned to the target state
		/// (still behind the loading screen, before the one-frame await / before fade-out).
		/// Parameters:
		/// - string enteredState: The entered game state ID.
		/// - string activeScene: The active scene name at the moment of state entry (may be empty).
		/// - GameStateSwitchReason reason: Why the switch was requested.
		/// </summary>
		public event Action<string, string, GameStateSwitchReason> EnteredStateEvent;

		private LoadingScreenService loadingScreenService;
		private GameData gameData;
		private CallbackService callbackService;
		private SceneService sceneService;

		private Coroutine coroutine;

		// Monotonic request id to prevent stale transitions / scene-load callbacks from winning races.
		private int switchVersion;

		public GameService(
			LoadingScreenService loadingScreenService,
			GameData gameData,
			IDependencyManager dependencyManager,
			CallbackService callbackService,
			SceneService sceneService)
		{
			this.loadingScreenService = loadingScreenService;
			this.gameData = gameData;
			this.callbackService = callbackService;
			this.sceneService = sceneService;

			EventSystem = GameObject.Instantiate(gameData.EventSystem);
			GameObject.DontDestroyOnLoad(EventSystem.gameObject);

			// Keep the brain purely for gameplay-level state (GAME/LOBBY/etc).
			Brain = new Brain(
				dependencyManager,
				callbackService,
				GameStateIdentifiers.LOADING,
				null,
				new List<StateMachineGraph>() { gameData.GameBrainGraph });

			Brain.Start();

			// Bootstrap into the correct state for the currently opened scene.
			if (gameData.Levels.ContainsKey(sceneService.CurrentScene))
			{
				// In game state.
				SwitchLevel(sceneService.CurrentScene, 0f, GameStateSwitchReason.Boot);
			}
			else
			{
				GameData.GameState state = gameData.StateData.Values.FirstOrDefault((s) => s.Scene == sceneService.CurrentScene);
				if (state == null)
				{
					SpaxDebug.Error("Game state could not be discerned from opened scene:", sceneService.CurrentScene);
				}
				else
				{
					// In non-game state.
					SwitchState(state.State, 0f, scene: "", reason: GameStateSwitchReason.Boot);
				}
			}
		}

		/// <summary>
		/// Switches the game's state to <paramref name="state"/>.
		/// </summary>
		/// <param name="state">The desired state to put the game in.</param>
		/// <param name="duration">The duration override for the loading screen UI transition. &lt;0 uses prefab defaults, 0 is immediate.</param>
		/// <param name="scene">An optional desired scene to load.</param>
		/// <param name="reason">Why the switch is happening.</param>
		public void SwitchState(
			string state,
			float duration = -1f,
			string scene = "",
			GameStateSwitchReason reason = GameStateSwitchReason.Unknown)
		{
			if (Brain.HeadState != null &&
				Brain.HeadState.ID == state &&
				(scene.IsNullOrEmpty() || sceneService.CurrentScene == scene))
			{
				// Already in desired state and scene.
				return;
			}

			// Supersede any prior switch request.
			switchVersion++;
			int version = switchVersion;

			// Retrieve desired scene.
			if (scene.IsNullOrEmpty() &&
				gameData.StateData.ContainsKey(state) &&
				gameData.StateData[state].HasScene)
			{
				scene = gameData.StateData[state].Scene;
			}

			SwitchRequestedEvent?.Invoke(state, scene, reason);

			SpaxDebug.Log($"Enter: [{state}]", "//" + scene);

			// 1) Fade loading screen IN (to black) first.
			// 2) Load scene (if needed) behind black.
			// 3) Enter the target state (spawns happen while still black).
			// 4) Wait 1 frame to allow enter logic/spawns to run.
			// 5) Fade loading screen OUT.
			if (duration.Approx(0f))
			{
				loadingScreenService.ShowImmediate();

				// Screen is now fully blocking the view.
				LoadingScreenShownEvent?.Invoke(state, scene, reason);

				BeginLoadAndEnterState(version, state, scene, reason);
			}
			else
			{
				loadingScreenService.Show(() =>
				{
					if (version != switchVersion)
					{
						return;
					}

					// Screen is now fully blocking the view.
					LoadingScreenShownEvent?.Invoke(state, scene, reason);

					BeginLoadAndEnterState(version, state, scene, reason, duration);
				}, duration);
			}
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads <paramref name="scene"/> as the active scene.
		/// </summary>
		public void SwitchLevel(string scene, float duration = -1f, GameStateSwitchReason reason = GameStateSwitchReason.LevelChange)
		{
			SwitchState(GameStateIdentifiers.GAME, duration, scene, reason);
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads the level at <paramref name="levelIndex"/> from <see cref="GameData.Levels"/>.
		/// </summary>
		public void SwitchLevel(int levelIndex, float duration = -1f, GameStateSwitchReason reason = GameStateSwitchReason.LevelChange)
		{
			if (levelIndex < 0 || levelIndex >= gameData.Levels.Count)
			{
				SpaxDebug.Error($"Level index out of range: {levelIndex}");
				return;
			}

			SwitchLevel(gameData.Levels.Keys.ElementAt(levelIndex), duration, reason);
		}

		private void BeginLoadAndEnterState(int version, string state, string scene, GameStateSwitchReason reason, float duration = -1f)
		{
			if (version != switchVersion)
			{
				return;
			}

			if (!scene.IsNullOrEmpty() && sceneService.CurrentScene != scene)
			{
				// Load new scene behind the loading screen.
				sceneService.LoadScene(scene, () =>
				{
					if (version != switchVersion)
					{
						return;
					}

					EnterTargetStateAndHide(version, state, scene, reason, duration);
				});
			}
			else
			{
				// Already in correct scene, enter new state.
				EnterTargetStateAndHide(version, state, scene, reason, duration);
			}
		}

		private void EnterTargetStateAndHide(int version, string state, string scene, GameStateSwitchReason reason, float duration = -1f)
		{
			if (version != switchVersion)
			{
				return;
			}

			bool transitioned = Brain.TryTransition(state);
			if (!transitioned)
			{
				SpaxDebug.Error("Failed to transition game state.", state);
			}

			EnteredStateEvent?.Invoke(state, sceneService.CurrentScene, reason);

			// Important: give the newly entered state's OnStateEntered/OnEnable spawners a frame to run
			// while we're still black, so the player/camera/UI are ready before fade-out begins.
			AwaitOneFrame(version, () =>
			{
				if (duration.Approx(0f))
				{
					loadingScreenService.HideImmediate();
				}
				else
				{
					loadingScreenService.Hide(null, duration);
				}
			});
		}

		private void AwaitOneFrame(int version, Action callback)
		{
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}

			if (version != switchVersion)
			{
				return;
			}

			coroutine = callbackService.StartCoroutine(AwaitOneFrameEnumerator(version, callback));
		}

		private IEnumerator AwaitOneFrameEnumerator(int version, Action callback)
		{
			yield return null;

			if (version != switchVersion)
			{
				yield break;
			}

			coroutine = null;
			callback?.Invoke();
		}
	}
}
