using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Timer struct that reads the current time and counts the distance from the start time.
	/// </summary>
	public struct TimerStruct
	{
		public float StartTime { get; }
		public float Duration { get; }
		public bool Realtime { get; }
		public float Speed { get; }
		public bool Paused
		{
			get
			{
				return paused;
			}
			set
			{
				if (value == paused)
				{
					return;
				}

				paused = value;
				if (paused)
				{
					pauseStart = CurrentTime;
				}
				else
				{
					AddDuration(CurrentTime - pauseStart);
				}
			}
		}
		public float Time => (CurrentTime - StartTime) * Speed;
		public float Remaining => (Duration + durationModifier) - Time;
		public float Progress => Mathf.Clamp01((Duration + durationModifier) == 0 ? 1f : Time / (Duration + durationModifier));
		public bool Expired => CurrentTime >= StartTime + (Duration + durationModifier);
		public bool Running => CurrentTime >= StartTime && !Expired;

		public float CurrentTime => Realtime ? UnityEngine.Time.realtimeSinceStartup : UnityEngine.Time.time;

		private float durationModifier;
		private bool paused;
		private float pauseStart;

		public TimerStruct(float duration, float startOffset, float speed = 1f, bool realtime = false)
		{
			StartTime = (realtime ? UnityEngine.Time.realtimeSinceStartup : UnityEngine.Time.time) - startOffset * (1f / speed);
			Duration = duration;
			Realtime = realtime;
			Speed = speed;

			durationModifier = 0f;
			paused = false;
			pauseStart = 0f;
			Paused = paused;
		}

		public TimerStruct(float duration = 0f, bool realtime = false) : this(duration, 0f, 1f, realtime) { }

		public void SubtractDuration(float time)
		{
			durationModifier -= time;
		}
		public void AddDuration(float time)
		{
			durationModifier += time;
		}

		public void Pause(bool pause = true)
		{
			Paused = pause;
		}

		public void Continue()
		{
			Pause(false);
		}

		public static implicit operator bool(TimerStruct timer)
		{
			return !timer.Expired;
		}
	}
}
