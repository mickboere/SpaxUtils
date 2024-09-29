using System;
using System.Collections.Generic;
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

		#region Timekeeping

		/// <summary>
		/// The initial <see cref="DateTime"/> when the currently loaded profile was created.
		/// If there is no currently loaded profile, time will be set to time of service instantiation.
		/// </summary>
		public DateTime InitialDateTime { get; private set; }

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
		public double UnscaledPlaytimeDouble { get { return (double)_playtimeUnscaled.Value; } private set { _playtimeUnscaled.Value = value; } }
		private RuntimeDataEntry _playtimeUnscaled;

		/// <summary>
		/// The <see cref="double"/> used for tracking <see cref="TimeType.ScaledPlaytime"/>.
		/// <para></para>
		/// <inheritdoc cref="TimeType.ScaledPlaytime"/>
		/// </summary>
		public double ScaledPlaytimeDouble { get { return (double)_playtimeScaled.Value; } private set { _playtimeScaled.Value = value; } }
		private RuntimeDataEntry _playtimeScaled;

		#endregion

		#endregion Timekeeping

		public bool Paused { get; private set; }

		private CallbackService callbackService;
		private RuntimeDataService runtimeDataService;

		private float originalTimeScale;
		private List<object> pauseRequesters = new List<object>();
		private List<object> pauseBlockers = new List<object>();

		public TimeService(CallbackService callbackService, RuntimeDataService runtimeDataService)
		{
			this.callbackService = callbackService;
			this.runtimeDataService = runtimeDataService;

			callbackService.UpdateCallback += Update;
			runtimeDataService.CurrentProfileChangedEvent += OnCurrentProfileChangedEvent;

			Load();
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= Update;
			runtimeDataService.CurrentProfileChangedEvent -= OnCurrentProfileChangedEvent;
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

		#region Pausing

		/// <summary>
		/// Will attempt pause the game.
		/// </summary>
		public bool RequestPause(object requester)
		{
			if (!pauseRequesters.Contains(requester))
			{
				pauseRequesters.Add(requester);
				if (pauseBlockers.Count == 0)
				{
					Pause(true);
				}
			}

			return pauseBlockers.Count == 0;
		}

		/// <summary>
		/// Complete a pause request and allow the game to continue if there are no pause requesters left.
		/// </summary>
		public void CompletePauseRequest(object requester)
		{
			if (pauseRequesters.Contains(requester))
			{
				pauseRequesters.Remove(requester);
				if (pauseRequesters.Count == 0)
				{
					Pause(false);
				}
			}
		}

		/// <summary>
		/// Add a blocker that prevents the game from pausing.
		/// </summary>
		public void AddPauseBlocker(object blocker)
		{
			if (!pauseBlockers.Contains(blocker))
			{
				pauseBlockers.Add(blocker);
				if (Paused)
				{
					Pause(false);
				}
			}
		}

		/// <summary>
		/// Remove a blocker to allow the game to pause again.
		/// </summary>
		public void RemovePauseBlocker(object blocker)
		{
			if (pauseBlockers.Contains(blocker))
			{
				pauseBlockers.Remove(blocker);
				if (pauseBlockers.Count == 0 && pauseRequesters.Count > 0)
				{
					Pause(true);
				}
			}
		}

		private void Pause(bool pause)
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
			}
		}

		#endregion Pausing

		private void Update()
		{
			UnscaledPlaytimeDouble += UnityEngine.Time.unscaledDeltaTime;
			ScaledPlaytimeDouble += UnityEngine.Time.deltaTime;
		}

		private void OnCurrentProfileChangedEvent(RuntimeDataCollection profile)
		{
			Load();
		}

		private void Load()
		{
			if (runtimeDataService.CurrentProfile != null)
			{
				RuntimeDataCollection metaData = runtimeDataService.GetMetaData();
				RuntimeDataEntry initialTimeEntry = metaData.GetEntry(GlobalDataIdentifiers.INITIAL_TIME,
					new RuntimeDataEntry(GlobalDataIdentifiers.INITIAL_TIME, DateTime.UtcNow.ToString()));
				if (DateTime.TryParse((string)initialTimeEntry.Value, out DateTime parse))
				{
					InitialDateTime = parse;
				}
				else
				{
					SpaxDebug.Error("Could not parse InitialDateTime!", (string)initialTimeEntry.Value);
				}

				_playtimeUnscaled = metaData.GetEntry(GlobalDataIdentifiers.PLAYTIME_UNSCALED,
					new RuntimeDataEntry(GlobalDataIdentifiers.PLAYTIME_UNSCALED, 0d));
				_playtimeScaled = metaData.GetEntry(GlobalDataIdentifiers.PLAYTIME_SCALED,
					new RuntimeDataEntry(GlobalDataIdentifiers.PLAYTIME_SCALED, 0d));
			}
			else
			{
				InitialDateTime = DateTime.UtcNow;
				_playtimeUnscaled = new RuntimeDataEntry(GlobalDataIdentifiers.PLAYTIME_UNSCALED, 0d);
				_playtimeScaled = new RuntimeDataEntry(GlobalDataIdentifiers.PLAYTIME_SCALED, 0d);
			}
		}
	}
}
