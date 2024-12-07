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
		private IDependencyManager dependencyManager;
		private CallbackService callbackService;
		private SceneService sceneService;

		private Coroutine coroutine;

		public GameService(GameData gameData, IDependencyManager dependencyManager, CallbackService callbackService, SceneService sceneService)
		{
			this.gameData = gameData;
			this.dependencyManager = dependencyManager;
			this.callbackService = callbackService;
			this.sceneService = sceneService;

			EventSystem = GameObject.Instantiate(gameData.EventSystem);
			GameObject.DontDestroyOnLoad(EventSystem.gameObject);
			Brain = new Brain(dependencyManager, callbackService, GameStateIdentifiers.LOADING, null, new List<StateMachineGraph>() { gameData.GameBrainGraph });
			Brain.Start();

			if (gameData.Levels.Contains(sceneService.CurrentScene))
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

			// Retrieve desired scene.
			if (scene.IsNullOrEmpty() &&
				gameData.StateData.ContainsKey(state) &&
				gameData.StateData[state].HasScene)
			{
				scene = gameData.StateData[state].Scene;
			}

			SpaxDebug.Log($"Enter: [{state}]", "//" + scene);

			// Create a new transition, NULL if immediate.
			ITransition transition = NewTransition();

			// Show loading screen.
			Brain.TryTransition(GameStateIdentifiers.LOADING, transition);
			AwaitTransition(transition, () =>
			{
				if (!scene.IsNullOrEmpty() && sceneService.CurrentScene != scene)
				{
					// Load new scene.
					sceneService.LoadScene(scene, () =>
					{
						// Enter new state.
						transition = NewTransition();
						Brain.TryTransition(state, transition);
					});
				}
				else
				{
					// Already in correct scene, enter new state.
					transition = NewTransition();
					Brain.TryTransition(state, transition);
				}
			});

			ITransition NewTransition()
			{
				return duration.Approx(0f) ? null :
					duration < 0f ?
						new TimedStateTransition(gameData.TransitionTime, true) :
						new TimedStateTransition(duration, true);
			}
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads <paramref name="scene"/> as the active scene.
		/// </summary>
		/// <param name="scene">The name of the scene you wish to load.</param>
		/// <param name="duration">The duration of the transition. <0 uses default value configured in <see cref="GameData.TransitionTime"/>, 0 is immediate.</param>
		public void SwitchLevel(string scene, float duration = -1f)
		{
			SwitchState(GameStateIdentifiers.GAME, duration, scene);
		}

		/// <summary>
		/// Switches game state to <see cref="GameStateIdentifiers.GAME"/> and loads the level at <paramref name="levelIndex"/> from <see cref="GameData.Levels"/>.
		/// </summary>
		/// <param name="levelIndex">The index of the level you wish to switch to.</param>
		/// <param name="duration">The duration of the transition. <0 uses default value configured in <see cref="GameData.TransitionTime"/>, 0 is immediate.</param>
		public void SwitchLevel(int levelIndex, float duration = -1f)
		{
			if (levelIndex < 0 || levelIndex >= gameData.Levels.Count)
			{
				SpaxDebug.Error($"Level index out of range: {levelIndex}");
				return;
			}

			SwitchLevel(gameData.Levels[levelIndex], duration);
		}

		private void AwaitTransition(ITransition transition, Action callback)
		{
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
			}

			if (transition == null)
			{
				callback?.Invoke();
			}
			else
			{
				coroutine = callbackService.StartCoroutine(AwaitEnumerator(transition, callback));
			}
		}

		private IEnumerator AwaitEnumerator(ITransition transition, Action callback)
		{
			while (!transition.Completed)
			{
				yield return null;
			}
			coroutine = null;
			callback?.Invoke();
		}
	}
}
