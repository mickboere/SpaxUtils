using UnityEngine;

namespace SpaxUtils
{
	public static class FloatExtensions
	{
		/// <summary>
		/// In-line version of <see cref="Mathf.Lerp(float, float, float)"/>.
		/// </summary>
		public static float LerpTo(this float a, float b, float t)
		{
			return Mathf.Lerp(a, b, t);
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
		/// Returns 1 - <paramref name="f"/> clamped between 0 and 1.
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
		/// Returns absolute of <paramref name="f"/>.
		/// </summary>
		public static float Abs(this float f)
		{
			return Mathf.Abs(f);
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
		public static float Range(this float f, float a, float b, float fromA = 0f, float fromB = 1f)
		{
			return Mathf.Lerp(a, b, Mathf.InverseLerp(fromA, fromB, f));
		}

		#region Easing functions

		#region Sine

		/// <summary>
		/// https://easings.net/#easeInOutSine
		/// </summary>
		public static float InOutSine(this float x)
		{
			return -(Mathf.Cos(Mathf.PI * x) - 1) / 2;
		}

		/// <summary>
		/// https://easings.net/#easeInOutSine
		/// </summary>
		public static float ReverseInOutSine(this float x)
		{
			return InOutSine(1f - x);
		}

		#endregion Sine

		#region Quad

		/// <summary>
		/// https://easings.net/#easeInQuad
		/// </summary>
		public static float InQuad(this float x)
		{
			return x * x;
		}

		/// <summary>
		/// https://easings.net/#easeOutQuad
		/// </summary>
		public static float OutQuad(this float x)
		{
			return 1f - (1f - x) * (1f - x);
		}

		/// <summary>
		/// https://easings.net/#easeInOutQuad
		/// </summary>
		public static float InOutQuad(this float x)
		{
			return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
		}

		#endregion Quad

		#region Cubic

		/// <summary>
		/// https://easings.net/#easeInCubic
		/// </summary>
		public static float InCubic(this float x)
		{
			return x * x * x;
		}

		/// <summary>
		/// https://easings.net/#easeOutCubic
		/// </summary>
		public static float OutCubic(this float x)
		{
			return 1f - Mathf.Pow(1f - x, 3f);
		}

		/// <summary>
		/// https://easings.net/#easeInOutCubic
		/// </summary>
		public static float InOutCubic(this float x)
		{
			return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
		}

		/// <summary>
		/// 1f - https://easings.net/#easeInOutCubic
		/// </summary>
		public static float ReverseInOutCubic(this float x)
		{
			return InOutCubic(1f - x);
		}

		#endregion Cubic

		#region Expo

		/// <summary>
		/// https://easings.net/#easeInExpo
		/// </summary>
		public static float InExpo(this float x)
		{
			return x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
		}

		/// <summary>
		/// https://easings.net/#easeOutExpo
		/// </summary>
		public static float OutExpo(this float x)
		{
			return x == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
		}

		#endregion Expo

		#endregion Easing Functions
	}
}
