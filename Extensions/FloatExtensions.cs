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

		#region Easing functions

		/// <summary>
		/// https://easings.net/#easeInQuad
		/// </summary>
		public static float InQuad(this float x)
		{
			return x * x;
		}

		/// <summary>
		/// https://easings.net/#easeInCubic
		/// </summary>
		public static float InCubic(this float x)
		{
			return x * x * x;
		}

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

		#endregion
	}
}
