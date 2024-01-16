using UnityEngine;

namespace SpaxUtils
{
	public struct Timer
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

		public Timer(float duration, float startOffset, float speed = 1f, bool realtime = false)
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

		public Timer(float duration = 0f, bool realtime = false) : this(duration, 0f, 1f, realtime) { }

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

		public static implicit operator bool(Timer timer)
		{
			return !timer.Expired;
		}
	}
}
