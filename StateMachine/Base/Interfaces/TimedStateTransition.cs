namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Basic timed state transition data container implementing <see cref="ITransition"/>.
	/// </summary>
	public class TimedStateTransition : ITransition
	{
		public float EntryProgress => timer.Progress;
		public float ExitProgress => 1f - EntryProgress;
		public bool Completed => timer.Expired;

		public float Duration { get; }

		private TimerStruct timer;

		public TimedStateTransition(float duration, bool realtime = false)
		{
			Duration = duration;
			timer = new TimerStruct(duration, 0f, 1f, realtime);
		}

		public void Dispose()
		{
		}
	}
}
