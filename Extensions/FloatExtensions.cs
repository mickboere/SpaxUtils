using UnityEngine;

namespace SpaxUtils
{
	public static class FloatExtensions
	{
		/// <summary>
		/// <see cref="Mathf.Approximately(float, float)"/> shorthand.
		/// </summary>
		public static bool Approx(this float a, float b)
		{
			return Mathf.Approximately(a, b);
		}

		/// <summary>
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

		public static float Invert(this float f)
		{
			return 1f - f;
		}

		public static float InvertClamp(this float f)
		{
			return 1f - Mathf.Clamp01(f);
		}

		#region Easing functions

		/// <summary>
		/// https://easings.net/#easeInOutCubic
		/// </summary>
		public static float InOutCubic(this float x)
		{
			return x < 0.5 ? 4 * x * x * x : 1 - Mathf.Pow(-2 * x + 2, 3) / 2;
		}

		/// <summary>
		/// 1f - https://easings.net/#easeInOutCubic
		/// </summary>
		public static float ReverseInOutCubic(this float x)
		{
			return InOutCubic(1f - x);
		}

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

		#endregion
	}
}
