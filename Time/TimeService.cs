using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service that keeps track of and controls time.
	/// TODO: Saving / loading
	/// </summary>
	public class TimeService : IService, IDisposable
	{
		/// <summary>
		/// Timescale set when game is paused.
		/// </summary>
		public const float PAUSED_TIMESCALE = 0.000001f;

		#region Floats

		/// <summary>
		/// <inheritdoc cref="TimeType.Realtime"/>
		/// </summary>
		public float Realtime => (float)RealtimeDouble;

		/// <summary>
		/// <inheritdoc cref="TimeType.UnscaledPlaytime"/>
		/// </summary>
		public float UnscaledPlaytime => (float)UnscaledPlaytimeDouble;

		/// <summary>
		/// <inheritdoc cref="TimeType.ScaledPlaytime"/>
		/// </summary>
		public float ScaledPlaytime => (float)ScaledPlaytimeDouble;

		#endregion

		#region Doubles

		/// <summary>
		/// The <see cref="double"/> used for tracking <see cref="TimeType.Realtime"/>.
		/// <para></para>
		/// <inheritdoc cref="TimeType.Realtime"/>
		/// </summary>
		public double RealtimeDouble => (DateTime.UtcNow - InitialDateTime).TotalSeconds;

		/// <summary>
		/// The <see cref="double"/> used for tracking <see cref="TimeType.UnscaledPlaytime"/>.
		/// <para></para>
		/// <inheritdoc cref="TimeType.UnscaledPlaytime"/>
		/// </summary>
		public double UnscaledPlaytimeDouble { get; private set; }

		/// <summary>
		/// The <see cref="double"/> used for tracking <see cref="TimeType.ScaledPlaytime"/>.
		/// <para></para>
		/// <inheritdoc cref="TimeType.ScaledPlaytime"/>
		/// </summary>
		public double ScaledPlaytimeDouble { get; private set; }

		#endregion

		public DateTime InitialDateTime { get; private set; }
		public bool Paused { get; private set; }

		private CallbackService callbackService;

		private float originalTimeScale;

		// TODO: Load time values from currently loaded profile (saving/loading data is also TODO)
		public TimeService(CallbackService callbackService)
		{
			this.callbackService = callbackService;

			callbackService.UpdateCallback += Update;

			Load();
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= Update;
		}

		/// <summary>
		/// Returns the current time of type <paramref name="timeType"/>.
		/// </summary>
		/// <param name="timeType">The type of time to request.</param>
		/// <returns>The time as a <see cref="float"/>.</returns>
		public float Time(TimeType timeType)
		{
			switch (timeType)
			{
				case TimeType.Realtime:
					return Realtime;
				case TimeType.UnscaledPlaytime:
					return UnscaledPlaytime;
				case TimeType.ScaledPlaytime:
					return ScaledPlaytime;
				default:
					Debug.LogError("TimeType not supported.");
					return 0f;
			}
		}

		/// <summary>
		/// Will attempt to set the current pause-state to <paramref name="pause"/>.
		/// </summary>
		/// <param name="pause">Whether the game should be paused or unpaused.</param>
		/// <returns></returns>
		public bool TryPause(bool pause)
		{
			if (Paused != pause)
			{
				Paused = pause;

				if (Paused)
				{
					originalTimeScale = UnityEngine.Time.timeScale;
					UnityEngine.Time.timeScale = PAUSED_TIMESCALE;
				}
				else
				{
					UnityEngine.Time.timeScale = originalTimeScale;
				}

				return true;
			}

			return false;
		}

		private void Update()
		{
			UnscaledPlaytimeDouble += UnityEngine.Time.unscaledDeltaTime;
			ScaledPlaytimeDouble += UnityEngine.Time.deltaTime;
		}

		private void Load()
		{
			InitialDateTime = DateTime.UtcNow;
		}
	}
}
