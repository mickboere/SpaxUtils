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

		private GameData gameData;
		private CallbackService callbackService;
		private SceneService sceneService;

		private Coroutine coroutine;

		// Monotonic request id to prevent stale transitions / scene-load callbacks from winning races.
		private int switchVersion;

		public GameService(GameData gameData, IDependencyManager dependencyManager, CallbackService callbackService, SceneService sceneService)
		{
			this.gameData = gameData;
			this.callbackService = callbackService;
			this.sceneService = sceneService;

			EventSystem = GameObject.Instantiate(gameData.EventSystem);
			GameObject.DontDestroyOnLoad(EventSystem.gameObject);
			Brain = new Brain(dependencyManager, callbackService, GameStateIdentifiers.LOADING, null, new List<StateMachineGraph>() { gameData.GameBrainGraph });
			Brain.Start();

			if (gameData.Levels.ContainsKey(sceneService.CurrentScene))
			{
				// In game state.
				SwitchLevel(sceneService.CurrentScene, 0f);
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
					SwitchState(state.State, 0f);
				}
			}
		}

		/// <summary>
		/// Switches the game's state to <paramref name="state"/>.
		/// </summary>
		/// <param name="state">The desired state to put the game in.</param>
		/// <param name="duration">The duration of the transition. <0 uses default value configured in <see cref="GameData.TransitionTime"/>, 0 is immediate.</param>
		/// <param name="scene">An optional desired scene to load.</param>
		public void SwitchState(string state, float duration = -1f, string scene = "")
		{
			if (Brain.HeadState.ID == state && (scene.IsNullOrEmpty() || sceneService.CurrentScene == scene))
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

			SpaxDebug.Log($"Enter: [{state}]", "//" + scene);

			// Create a new transition, NULL if immediate.
			ITransition transition = NewTransition(duration);

			// Show loading screen.
			Brain.TryTransition(GameStateIdentifiers.LOADING, transition);
			AwaitTransition(version, transition, () =>
			{
				if (version != switchVersion)
				{
					return;
				}

				if (!scene.IsNullOrEmpty() && sceneService.CurrentScene != scene)
				{
					// Load new scene.
					sceneService.LoadScene(scene, () =>
					{
						if (version != switchVersion)
						{
							return;
						}

						// Enter new state.
						ITransition enterTransition = NewTransition(duration);
						Brain.TryTransition(state, enterTransition);
					});
				}
				else
				{
					// Already in correct scene, enter new state.
					ITransition enterTransition = NewTransition(duration);
					Brain.TryTransition(state, enterTransition);
				}
			});
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads <paramref name="scene"/> as the active scene.
		/// </summary>
		/// <param name="scene">The name of the scene you wish to load.</param>
		/// <param name="duration">The duration of the transition. <-1 uses default value configured in <see cref="GameData.TransitionTime"/>, 0 is immediate.</param>
		public void SwitchLevel(string scene, float duration = -1f)
		{
			SwitchState(GameStateIdentifiers.GAME, duration, scene);
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads the level at <paramref name="levelIndex"/> from <see cref="GameData.Levels"/>.
		/// </summary>
		/// <param name="levelIndex">The index of the level you wish to switch to.</param>
		/// <param name="duration">The duration of the transition. <-1 uses default value configured in <see cref="GameData.TransitionTime"/>, 0 is immediate.</param>
		public void SwitchLevel(int levelIndex, float duration = -1f)
		{
			if (levelIndex < 0 || levelIndex >= gameData.Levels.Count)
			{
				SpaxDebug.Error($"Level index out of range: {levelIndex}");
				return;
			}

			SwitchLevel(gameData.Levels.Keys.ElementAt(levelIndex), duration);
		}

		private ITransition NewTransition(float duration)
		{
			if (duration.Approx(0f))
			{
				return null;
			}

			float t = duration < 0f ? gameData.TransitionTime : duration;
			return new TimedStateTransition(t, true);
		}

		private void AwaitTransition(int version, ITransition transition, Action callback)
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

			if (transition == null)
			{
				callback?.Invoke();
			}
			else
			{
				coroutine = callbackService.StartCoroutine(AwaitEnumerator(version, transition, callback));
			}
		}

		private IEnumerator AwaitEnumerator(int version, ITransition transition, Action callback)
		{
			while (!transition.Completed)
			{
				if (version != switchVersion)
				{
					yield break;
				}

				yield return null;
			}

			if (version != switchVersion)
			{
				yield break;
			}

			coroutine = null;
			callback?.Invoke();
		}
	}
}
