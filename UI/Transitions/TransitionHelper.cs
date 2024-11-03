using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Helper class that handles a (0-1) clamped float transition.
	/// Has support for animation curves.
	/// </summary>
	public class TransitionHelper : IDisposable
	{
		public event Action ProgressedEvent;
		public event Action EmptiedEvent;
		public event Action FilledEvent;

		public bool IsFull => Progress.Approx(1f);
		public bool IsEmpty => Progress.Approx(0f);
		public bool Still => IsFull || IsEmpty;
		public bool Transitioning => !Still;

		/// <summary>
		/// The animation curves evaluation of the current progress.
		/// </summary>
		public float Evaluation =>
			Control > 0 ?
				(intro != null && intro.keys.Length > 0 ? intro.Evaluate(Progress) : Progress) :
				(outro != null && outro.keys.Length > 0 ? outro.Evaluate(Progress) : Progress);

		/// <summary>
		/// The amount of progression per second.
		/// </summary>
		public float Control { get; set; }

		/// <summary>
		/// The current progress between 0 and 1.
		/// </summary>
		public float Progress
		{
			get { return progress; }
			set
			{
				progress = Mathf.Clamp01(value);
				OnProgressed();
			}
		}

		protected float Now => realtime ? Time.realtimeSinceStartup : Time.time;

		private readonly bool realtime;
		private readonly float relativeDelay;
		private readonly float inTime;
		private readonly float outTime;
		private readonly AnimationCurve intro;
		private readonly AnimationCurve outro;

		private float progress;
		private float startTime;
		private float lastUpdate;
		private Action callback;

		public TransitionHelper(bool realtime = true, float relativeDelay = 1f, float inTime = 1f, float outTime = 1f, AnimationCurve intro = null, AnimationCurve outro = null)
		{
			this.realtime = realtime;
			this.relativeDelay = relativeDelay;
			this.inTime = inTime;
			this.outTime = outTime;
			this.intro = intro;
			this.outro = outro;
		}

		public TransitionHelper(TransitionSettings settings)
		{
			this.realtime = settings.Realtime;
			this.relativeDelay = settings.RelativeDelay;
			this.inTime = settings.InTime;
			this.outTime = settings.OutTime;
			this.intro = settings.Intro;
			this.outro = settings.Outro;
		}

		public void Dispose()
		{
		}

		public void Transition(bool fill, Action callback = null, float delay = 0f, float overrideDuration = -1f)
		{
			float duration = overrideDuration < 0f ? (fill ? inTime : outTime) : overrideDuration;
			Control = 1f / duration * (fill ? 1f : -1f);
			startTime = Now + delay * relativeDelay;
			this.callback = callback;

			if (fill && IsFull || !fill && IsEmpty)
			{
				// Transition is already at the desired progress, reset it to invoke all events.
				Progress = fill ? 1f : 0f;
			}
		}

		public virtual void Fill(Action callback = null, float delay = 0f, float overrideTime = -1f)
		{
			Transition(true, callback, delay, overrideTime);
		}

		public virtual void Empty(Action callback = null, float delay = 0f, float overrideTime = -1f)
		{
			Transition(false, callback, delay, overrideTime);
		}

		public virtual void FillImmediately()
		{
			startTime = Now;
			Progress = 1f;
		}

		public virtual void EmptyImmediately()
		{
			startTime = Now;
			Progress = 0f;
		}

		/// <summary>
		/// Will try to update the Progress value depending on whether there is currently a transition going on.
		/// </summary>
		/// <returns></returns>
		public bool TryUpdateProgress()
		{
			float time = Now;
			if (time < startTime)
			{
				// Await delay.
				return false;
			}

			float change = Control * (time - lastUpdate);
			float newProgress = Mathf.Clamp01(Progress + change);
			lastUpdate = time;
			if (newProgress.Approx(Progress))
			{
				// No change in progress detected; don't alter value -> don't invoke events.
				return false;
			}

			Progress = newProgress;
			return true;
		}

		protected virtual void OnProgressed()
		{
			ProgressedEvent?.Invoke();

			if (Still && callback != null)
			{
				callback();
				callback = null;
			}

			if (IsFull)
			{
				progress = 1f;
				FilledEvent?.Invoke();
			}
			else if (IsEmpty)
			{
				progress = 0f;
				EmptiedEvent?.Invoke();
			}
		}
	}
}
