using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace SpaxUtils
{
	/// <summary>
	/// Service that orchestrates cutscene playback.
	/// Manages <see cref="CutsceneDirector"/> registration, play/skip lifecycle,
	/// and coordinates screen fading via <see cref="ScreenFadeService"/>.
	/// Fires events for other systems (agent brain nodes, UI bridges) to respond to.
	/// </summary>
	public class CutsceneService : IService
	{
		/// <summary>
		/// Fired when a cutscene begins playing.
		/// </summary>
		public event Action CutsceneStartedEvent;

		/// <summary>
		/// Fired after a cutscene has fully ended (fade complete, cleanup done).
		/// </summary>
		public event Action CutsceneEndedEvent;

		/// <summary>
		/// Whether a cutscene is currently in progress (including fade-out).
		/// </summary>
		public bool Playing { get; private set; }

		private ScreenFadeService screenFadeService;

		private Dictionary<string, CutsceneDirector> directors = new Dictionary<string, CutsceneDirector>();
		private CutsceneDirector activeDirector;
		private Action onComplete;
		private bool completing;

		public CutsceneService(ScreenFadeService screenFadeService)
		{
			this.screenFadeService = screenFadeService;
		}

		/// <summary>
		/// Register a <see cref="CutsceneDirector"/> so it can be resolved by key at runtime.
		/// </summary>
		public void Register(string key, CutsceneDirector director)
		{
			if (directors.ContainsKey(key))
			{
				SpaxDebug.Warning($"CutsceneDirector already registered for key '{key}'. Overwriting.");
			}
			directors[key] = director;
		}

		/// <summary>
		/// Unregister a <see cref="CutsceneDirector"/> by key.
		/// </summary>
		public void Unregister(string key)
		{
			directors.Remove(key);
		}

		/// <summary>
		/// Play the cutscene registered under <paramref name="key"/>.
		/// </summary>
		/// <param name="key">The registration key matching a <see cref="CutsceneDirector"/> in the scene.</param>
		/// <param name="onComplete">Invoked after the cutscene has fully ended and the screen is faded to black,
		/// just before the fade-back-in begins.</param>
		public void Play(string key, Action onComplete = null)
		{
			if (Playing)
			{
				SpaxDebug.Error("Cutscene already playing.", $"Attempted to play '{key}' while another cutscene is active.");
				return;
			}

			if (!directors.TryGetValue(key, out CutsceneDirector director))
			{
				SpaxDebug.Error("CutsceneDirector not found.", $"No director registered for key '{key}'.");
				onComplete?.Invoke();
				return;
			}

			this.onComplete = onComplete;
			activeDirector = director;
			completing = false;
			Playing = true;

			director.Director.stopped += OnDirectorStopped;
			CutsceneStartedEvent?.Invoke();
			director.Director.Play();
		}

		/// <summary>
		/// Skip the currently playing cutscene. Fades to black before completing.
		/// </summary>
		public void Skip()
		{
			if (!Playing || completing)
			{
				return;
			}
			Complete();
		}

		private void OnDirectorStopped(PlayableDirector director)
		{
			director.stopped -= OnDirectorStopped;
			if (!completing)
			{
				Complete();
			}
		}

		/// <summary>
		/// Unified completion path for both natural end and skip.
		/// Fades to black (or hands off instantly if the timeline already ends on black),
		/// cleans up, fires events, then fades back in.
		/// </summary>
		private void Complete()
		{
			completing = true;

			// Stop the director if it is still playing (skip path).
			if (activeDirector != null && activeDirector.Director.state == PlayState.Playing)
			{
				activeDirector.Director.stopped -= OnDirectorStopped;
				activeDirector.Director.Stop();
			}

			if (activeDirector != null && activeDirector.EndsOnBlack)
			{
				// Timeline already ends on black; instant handoff to the fade service.
				screenFadeService.ShowImmediate();
				FinalizeAndFadeIn();
			}
			else
			{
				// Timeline does not end on black; animate the fade.
				screenFadeService.Show(() =>
				{
					FinalizeAndFadeIn();
				});
			}
		}

		/// <summary>
		/// Shared finalization: cleans up the director, fires events, then fades back in.
		/// Must only be called while the screen fade service is fully black.
		/// </summary>
		private void FinalizeAndFadeIn()
		{
			CutsceneDirector director = activeDirector;

			Playing = false;
			completing = false;
			activeDirector = null;

			// Reset any scene-side state (e.g. fade overlay) while hidden behind the fade service.
			if (director != null)
			{
				director.Cleanup();
			}

			Action callback = onComplete;
			onComplete = null;

			CutsceneEndedEvent?.Invoke();
			callback?.Invoke();

			// Fade back in while the player already has control.
			screenFadeService.Hide();
		}
	}
}
