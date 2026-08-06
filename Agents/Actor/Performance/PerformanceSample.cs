namespace SpaxUtils
{
	/// <summary>
	/// The complete mutable state of a performance at one instant. Plain data, so the runtime performer and
	/// an editor preview can advance identical logic and never disagree.
	/// </summary>
	public struct PerformanceSample
	{
		public PerformanceState State;
		public float ChargeTime;
		public float RunTime;
		public float CancelTime;
		public float Weight;

		public static PerformanceSample Create(IPerformanceMove move)
		{
			return new PerformanceSample
			{
				State = move.HasCharge ? PerformanceState.Preparing : PerformanceState.Performing,
				ChargeTime = 0f,
				RunTime = 0f,
				CancelTime = 0f,
				Weight = 0f
			};
		}

		public override string ToString()
		{
			return $"PerformanceSample({State}, charge={ChargeTime:0.000}, run={RunTime:0.000}, weight={Weight:0.00})";
		}
	}
}
