using System;

namespace SpaxUtils
{
	[Flags]
	public enum PerformanceState
	{
		Inactive = 1 << 0,
		Preparing = 1 << 1,
		Performing = 1 << 2,
		Finishing = 1 << 3,
		Completed = 1 << 4
	}
}
