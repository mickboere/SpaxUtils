using UnityEngine;

namespace SpaxUtils
{
	public static class FloatExtensions
	{
		#region Math

		/// <summary>
		/// In-line version of <see cref="Mathf.Lerp(float, float, float)"/>.
		/// </summary>
		public static float Lerp(this float a, float b, float t)
		{
			return Mathf.Lerp(a, b, t);
		}

		/// <summary>
		/// In-line version of <see cref="Mathf.InverseLerp(float, float, float)(float, float, float)"/>.
		/// </summary>
		public static float InverseLerp(this float t, float a, float b)
		{
			return Mathf.InverseLerp(a, b, t);
		}

		/// <summary>
		/// Lerps from <paramref name="v2"/>.x to <paramref name="v2"/>.y by <paramref name="t"/>.
		/// </summary>
		public static float Lerp(this Vector2 v2, float t)
		{
			return v2.x.Lerp(v2.y, t);
		}

		/// <summary>
		/// In-line version of <see cref="Mathf.Approximately(float, float)"/>.
		/// </summary>
		public static bool Approx(this float a, float b)
		{
			return Mathf.Approximately(a, b);
		}

		/// <summary>
		/// In-line version of <see cref="Mathf.Repeat(float, float)"/>.
		/// Loops <paramref name="f"/> around to be between 0 and <paramref name="max"/>.
		/// </summary>
		public static float Repeat(this float f, float max = 1f)
		{
			return Mathf.Repeat(f, max);
		}

		/// <summary>
		/// Remaps (0-1) to (-overshoot, 1+overshoot)
		/// </summary>
		/// <param name="f">The base normalized value to overshoot.</param>
		/// <param name="overshot">The amount with which to overshoot outside of the normalized range.</param>
		public static float OverShoot(this float f, float overshot)
		{
			return f * (1f + overshot * 2f) - overshot;
		}

		/// <summary>
		/// Returns 1 - <paramref name="f"/>.
		/// </summary>
		public static float Invert(this float f)
		{
			return 1f - f;
		}

		/// <summary>
		/// Clamps <paramref name="f"/> between 0 and 1 and returns it inverted.
		/// </summary>
		public static float InvertClamped(this float f)
		{
			return 1f - Mathf.Clamp01(f);
		}

		/// <summary>
		/// Returns <paramref name="f"/> clamped between 0 and 1.
		/// </summary>
		public static float Clamp01(this float f)
		{
			return Mathf.Clamp01(f);
		}

		/// <summary>
		/// Returns <paramref name="f"/> clamped between <paramref name="min"/> and <paramref name="max"/>.
		/// </summary>
		public static float Clamp(this float f, float min, float max)
		{
			return Mathf.Clamp(f, min, max);
		}

		/// <summary>
		/// Returns absolute of <paramref name="f"/>.
		/// </summary>
		public static float Abs(this float f)
		{
			return Mathf.Abs(f);
		}

		/// <summary>
		/// Returns the lowest value between <paramref name="f"/> and <paramref name="values"/>.
		/// </summary>
		public static float Min(this float f, params float[] values)
		{
			return Mathf.Min(f, Mathf.Min(values));
		}

		/// <summary>
		/// Returns the highest value between <paramref name="f"/> and <paramref name="values"/>.
		/// </summary>
		public static float Max(this float f, params float[] values)
		{
			return Mathf.Max(f, Mathf.Max(values));
		}

		/// <summary>
		/// Remaps value <paramref name="f"/> to a different range.
		/// </summary>
		/// <param name="f">The value to remap.</param>
		/// <param name="a">The new minimum value.</param>
		/// <param name="b">The new maximum value.</param>
		/// <param name="fromA">The previous minimum value.</param>
		/// <param name="fromB">The previous maximum value.</param>
		/// <returns></returns>
		public static float Remap(this float f, float a, float b, float fromA = 0f, float fromB = 1f)
		{
			return Mathf.Lerp(a, b, Mathf.InverseLerp(fromA, fromB, f));
		}

		public static float Sqrt(this float f)
		{
			return Mathf.Sqrt(f);
		}

		public static float Pow(this float f, float p = 2f)
		{
			return Mathf.Pow(f, p);
		}

		#endregion Math

		#region Rounding

		public static float Round(this float f)
		{
			return Mathf.Round(f);
		}

		public static int RoundToInt(this float f)
		{
			return Mathf.RoundToInt(f);
		}

		public static float Floor(this float f)
		{
			return Mathf.Floor(f);
		}

		public static int FloorToInt(this float f)
		{
			return Mathf.FloorToInt(f);
		}

		public static float Ceil(this float f)
		{
			return Mathf.Ceil(f);
		}

		public static float Decimal(this float f, DecimalMethod method)
		{
			switch (method)
			{
				case DecimalMethod.Floor:
					return Mathf.Floor(f);
				case DecimalMethod.Round:
					return Mathf.Round(f);
				case DecimalMethod.Ceil:
					return Mathf.Ceil(f);
				case DecimalMethod.Decimal:
				default:
					return f;
			}
		}

		#endregion Rounding

		/// <summary>
		/// Uses this float to evaluate <see cref="AnimationCurve"/> <paramref name="curve"/>.
		/// </summary>
		public static float Evaluate(this float f, AnimationCurve curve)
		{
			return curve.Evaluate(f);
		}

		/// <summary>
		/// Converts seconds in float value to milliseconds in integer value.
		/// </summary>
		public static int ToMilliseconds(this float seconds)
		{
			return Mathf.RoundToInt(seconds * 1000f);
		}

		/// <summary>
		/// Converts <paramref name="f"/> into a percentage.
		/// </summary>
		/// <param name="f"></param>
		/// <returns></returns>
		public static int ToPercentage(this float f)
		{
			return Mathf.RoundToInt(f * 100f);
		}
	}
}
