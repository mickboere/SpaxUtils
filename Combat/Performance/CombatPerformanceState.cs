using System;

namespace SpaxUtils
{
	[Flags]
	public enum CombatPerformanceState
	{
		Charging = 1 << 0,
		Released = 1 << 1,
		Finishing = 1 << 2,
		Completed = 1 << 3
	}
}
