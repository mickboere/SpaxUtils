namespace SpaxUtils
{
	/// <summary>
	/// One frame of input to <see cref="PerformanceClock"/>, with time deltas already scaled by the caller.
	/// </summary>
	public struct PerformanceClockInput
	{
		/// <summary>Frame delta scaled by the phase speed multiplier and entity timescale.</summary>
		public float Delta;

		/// <summary>Frame delta scaled by entity timescale only; cancelling ignores the speed multiplier.</summary>
		public float CancelDelta;

		public bool Released;
		public bool Prolong;
		public bool Paused;

		/// <summary>
		/// Resolves the phase multiplier from the state at ENTRY, so a frame that completes charging carries
		/// the charge multiplier into its performance step - exactly as the original inline clock did.
		/// </summary>
		public static PerformanceClockInput Create(PerformanceSample sample, float deltaTime, float chargeSpeed,
			float performSpeed, float timeScale, bool released, bool prolong, bool paused)
		{
			float speed = sample.State == PerformanceState.Preparing ? chargeSpeed : performSpeed;
			return new PerformanceClockInput
			{
				Delta = deltaTime * speed * timeScale,
				CancelDelta = deltaTime * timeScale,
				Released = released,
				Prolong = prolong,
				Paused = paused
			};
		}
	}
}
