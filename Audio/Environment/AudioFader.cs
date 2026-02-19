using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpaxUtils
{
	public class AudioFader : IDisposable
	{
		public AudioSource CurrentAudioSource;
		public AudioSource PreviousAudioSource;

		private TransitionHelper currentTransition;
		private TransitionHelper previousTransition;

		private AudioSource a;
		private AudioSource b;
		private CallbackService callbackService;

		public AudioFader(AudioSource a, AudioSource b, CallbackService callbackService)
		{
			this.a = a;
			this.b = b;
			this.callbackService = callbackService;

			PreviousAudioSource = a;
			CurrentAudioSource = b;

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate);
		}

		public void Dispose()
		{
			previousTransition?.Dispose();
			currentTransition?.Dispose();
			if (a) Object.Destroy(a.gameObject);
			if (b) Object.Destroy(b.gameObject);
			callbackService.UnsubscribeUpdates(this);
		}

		public void Fade(AudioClip clip, TransitionSettings transitionSettings, float delay = 0f, bool loop = true, float startTime = 0f)
		{
			if (!CurrentAudioSource || CurrentAudioSource.clip == clip)
			{
				// Destroyed |or| Already fading to this clip.
				return;
			}

			if (previousTransition != null)
			{
				// Already fading out previous.
				if (PreviousAudioSource.clip == clip)
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

			if (CurrentAudioSource) // OnDestroy edgecase
			{
				CurrentAudioSource.Stop();
				CurrentAudioSource.volume = 0f;
				CurrentAudioSource.clip = clip;
				CurrentAudioSource.loop = loop;

				currentTransition = transitionSettings != null ? new TransitionHelper(transitionSettings) : new TransitionHelper();
				float trueDelay = delay + (previousTransition == null ? 0f : previousTransition.TimeRemaining);
				currentTransition.Fill(delay: trueDelay);

				if (clip != null)
				{
					CurrentAudioSource.time = startTime;
					CurrentAudioSource.PlayDelayed(trueDelay * currentTransition.RelativeDelay);
				}
			}
		}

		protected void OnUpdate(float delta)
		{
			Update(PreviousAudioSource, previousTransition);
			Update(CurrentAudioSource, currentTransition);
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
			if (PreviousAudioSource == a)
			{
				PreviousAudioSource = b;
				CurrentAudioSource = a;
			}
			else
			{
				PreviousAudioSource = a;
				CurrentAudioSource = b;
			}
		}

		private void FadeoutPrevious()
		{
			previousTransition.Empty(() =>
			{
				if (PreviousAudioSource)
				{
					PreviousAudioSource.Stop();
				}
				previousTransition.Dispose();
				previousTransition = null;
			});
		}
	}
}
