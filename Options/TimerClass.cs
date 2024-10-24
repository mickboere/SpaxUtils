﻿using System;

namespace SpaxUtils
{
	/// <summary>
	/// Timer instance class that counts it own time and supports custom timescales.
	/// </summary>
	public class TimerClass : IDisposable
	{
		public Action TimerExpiredEvent;

		public float Time { get; set; }
		public float? Duration { get; set; }
		public float Timescale
		{
			get { return TimescaleFunc == null ? _timescale : TimescaleFunc(); }
			set { _timescale = value; TimescaleFunc = null; }
		}
		private float _timescale = 1f;
		private Func<float> TimescaleFunc { get; set; }

		public float Progress
		{
			get { return Duration.HasValue ? Time / Duration.Value : 1f; }
			set { if (Duration.HasValue) { Time = Duration.Value * value; } }
		}
		public bool Expired => Duration.HasValue ? Time >= Duration.Value : true;

		private CallbackService callbackService;

		public TimerClass(float? duration = null, CallbackService callbackService = null)
		{
			Duration = duration;
			if (callbackService != null)
			{
				this.callbackService = callbackService;
				callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, -9999);
			}
		}

		public TimerClass(float? duration, float timescale = 1f, CallbackService callbackService = null) : this(duration, callbackService)
		{
			_timescale = timescale;
		}

		public TimerClass(float? duration, Func<float> timescale = null, CallbackService callbackService = null) : this(duration, callbackService)
		{
			TimescaleFunc = timescale;
		}

		public TimerClass(float? duration, float timescale = 1f, bool autoUpdate = false) :
			this(duration, timescale, autoUpdate ? GlobalDependencyManager.Instance.Get<CallbackService>() : null)
		{ }

		public TimerClass(float? duration, Func<float> timescale = null, bool autoUpdate = false) :
			this(duration, timescale, autoUpdate ? GlobalDependencyManager.Instance.Get<CallbackService>() : null)
		{ }

		public void Dispose()
		{
			if (callbackService != null)
			{
				callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
			}
		}

		/// <summary>
		/// Updates the timer by adding <paramref name="delta"/> to <see cref="Time"/>.
		/// </summary>
		/// <param name="delta">The delta time to add to <see cref="Time"/></param>
		/// <returns>Whether the timer has expired.</returns>
		public bool Update(float delta)
		{
			bool wasExpired = Expired;
			Time += delta * Timescale;
			if (!wasExpired && Expired)
			{
				TimerExpiredEvent?.Invoke();
			}
			return Expired;
		}

		/// <summary>
		/// Resets the timer to 0.
		/// </summary>
		public TimerClass Reset()
		{
			Time = 0f;
			return this;
		}

		/// <summary>
		/// Resets the timer.
		/// </summary>
		/// <param name="duration">Expiration time of the timer. Leave null to remove duration for an infinite timer.</param>
		/// <param name="time"></param>
		public TimerClass Reset(float? duration, float time = 0f)
		{
			Duration = duration;
			Time = time;
			return this;
		}

		private void OnUpdate(float delta)
		{
			Update(delta);
		}
	}
}
