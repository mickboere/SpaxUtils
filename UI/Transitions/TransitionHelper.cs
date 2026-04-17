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

		/// <summary>
		/// Whether the progress is currently at 1.
		/// </summary>
		public bool IsFull => Progress.Approx(1f);

		/// <summary>
		/// Whether the transition is currently being filled.
		/// </summary>
		public bool IsFilling => !IsFull && Control > 0;

		/// <summary>
		/// Whether the progress is currently at 0.
		/// </summary>
		public bool IsEmpty => Progress.Approx(0f);

		/// <summary>
		/// Whether the transition is currently being emptied.
		/// </summary>
		public bool IsEmptying => !IsEmpty && Control < 0;

		/// <summary>
		/// Whether the progress is currently either at 0 or 1.
		/// </summary>
		public bool Completed => IsFull || IsEmpty;

		/// <summary>
		/// Whether the progress is currently anywhere between 0 and 1.
		/// </summary>
		public bool Transitioning => !Completed;

		/// <summary>
		/// Whether this transition has been full before.
		/// </summary>
		public bool WasFull { get; private set; }

		/// <summary>
		/// The animation curves evaluation of the current progress.
		/// </summary>
		public float Evaluation =>
			Control > 0 || !WasFull ?
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

		/// <summary>
		/// How many seconds until the transition reaches its target state.
		/// </summary>
		public float TimeRemaining =>
			Control > 0f ?
				Progress.Invert() / Control :
				Progress / -Control;

		protected float Now => Realtime ? Time.realtimeSinceStartup : Time.time;

		public readonly bool Realtime;
		public readonly float RelativeDelay;
		public readonly float InTime;
		public readonly float OutTime;

		private readonly AnimationCurve intro;
		private readonly AnimationCurve outro;

		private float progress;
		private float startTime;
		private float lastUpdate;
		private Action callback;

		public TransitionHelper(bool realtime = true, float relativeDelay = 1f, float inTime = 1f, float outTime = 1f, AnimationCurve intro = null, AnimationCurve outro = null)
		{
			this.Realtime = realtime;
			this.RelativeDelay = relativeDelay;
			this.InTime = inTime;
			this.OutTime = outTime;
			this.intro = intro;
			this.outro = outro;
		}

		public TransitionHelper(TransitionSettings settings)
		{
			this.Realtime = settings.Realtime;
			this.RelativeDelay = settings.RelativeDelay;
			this.InTime = settings.InTime;
			this.OutTime = settings.OutTime;
			this.intro = settings.Intro;
			this.outro = settings.Outro;
		}

		public void Dispose()
		{
		}

		public void Transition(bool fill, Action callback = null, float delay = 0f, float overrideDuration = -1f)
		{
			float duration = overrideDuration < 0f ? (fill ? InTime : OutTime) : overrideDuration;
			Control = 1f / duration * (fill ? 1f : -1f);
			startTime = Now + delay * RelativeDelay;
			lastUpdate = startTime;
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

			if (Completed && callback != null)
			{
				callback();
				callback = null;
			}

			if (!WasFull && IsFull)
			{
				WasFull = true;
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
