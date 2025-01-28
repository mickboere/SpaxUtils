using System;
using UnityEngine;

namespace SpaxUtils
{
	public class AudioFader : IDisposable
	{
		private AudioSource a;
		private AudioSource b;
		private CallbackService callbackService;

		private TransitionHelper previousTransition;
		private AudioSource previousAudioSource;
		private TransitionHelper currentTransition;
		private AudioSource currentAudioSource;

		public AudioFader(AudioSource a, AudioSource b, CallbackService callbackService)
		{
			this.a = a;
			this.b = b;
			this.callbackService = callbackService;

			previousAudioSource = a;
			currentAudioSource = b;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public void Dispose()
		{
			previousTransition?.Dispose();
			currentTransition?.Dispose();
			callbackService.UnsubscribeUpdates(this);
		}

		public void Fade(AudioClip clip, TransitionSettings transitionSettings, float delay = 0f, bool loop = true, float startTime = 0f)
		{
			if (!currentAudioSource || currentAudioSource.clip == clip)
			{
				// Destroyed |or| Already fading to this clip.
				return;
			}

			if (previousTransition != null)
			{
				// Already fading out previous.
				if (previousAudioSource.clip == clip)
				{
					// Clips match, revert progress.
					SwitchSources();
					TransitionHelper temp = currentTransition;
					currentTransition = previousTransition;
					previousTransition = temp;
					FadeoutPrevious();
					currentTransition.Fill();
					return;
				}
				else
				{
					// overwrite current.
					currentTransition?.Dispose();
				}
			}
			else if (currentTransition != null)
			{
				// Fade out current as previous.
				SwitchSources();
				previousTransition = currentTransition;
				FadeoutPrevious();
			}
			// Else: fresh initialize.

			currentAudioSource.Stop();
			currentAudioSource.volume = 0f;
			currentAudioSource.clip = clip;
			currentAudioSource.loop = loop;
			currentAudioSource.time = startTime;

			currentTransition = transitionSettings != null ? new TransitionHelper(transitionSettings) : new TransitionHelper();
			float trueDelay = delay + (previousTransition == null ? 0f : previousTransition.TimeRemaining);
			currentTransition.Fill(delay: trueDelay);
			currentAudioSource.PlayDelayed(trueDelay * currentTransition.RelativeDelay);
		}

		protected void OnUpdate(float delta)
		{
			Update(previousAudioSource, previousTransition);
			Update(currentAudioSource, currentTransition);
		}

		private void Update(AudioSource source, TransitionHelper transition)
		{
			if (transition != null && transition.TryUpdateProgress())
			{
				source.volume = transition.Evaluation;
			}
		}

		private void SwitchSources()
		{
			if (previousAudioSource == a)
			{
				previousAudioSource = b;
				currentAudioSource = a;
			}
			else
			{
				previousAudioSource = a;
				currentAudioSource = b;
			}
		}

		private void FadeoutPrevious()
		{
			previousTransition.Empty(() =>
			{
				if (previousAudioSource)
				{
					previousAudioSource.Stop();
				}
				previousTransition.Dispose();
				previousTransition = null;
			});
		}
	}
}
