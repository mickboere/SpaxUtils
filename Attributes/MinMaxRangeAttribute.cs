using UnityEngine;

namespace SpaxUtils
{
	public class MinMaxRangeAttribute : PropertyAttribute
	{
		public float Min { get; }
		public float Max { get; }
		public bool ClampMin { get; }
		public bool ClampMax { get; }

		public MinMaxRangeAttribute(float min, float max, bool clamp = false)
		{
			Min = min;
			Max = max;
			ClampMin = clamp;
			ClampMin = clamp;
		}

		public MinMaxRangeAttribute(float min, float max, bool clampMin, bool clampMax)
		{
			Min = min;
			Max = max;
			ClampMin = clampMin;
			ClampMin = clampMax;
		}
	}
}
