using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class containing a lot of easing method float extensions.
	/// </summary>
	public static class Easing
	{
		public static float Ease(this float f, EasingMethod method)
		{
			switch (method)
			{
				// Sine
				case EasingMethod.InSine:
					return f.InSine();
				case EasingMethod.OutSine:
					return f.OutSine();
				case EasingMethod.InOutSine:
					return f.InOutSine();
				// Quad
				case EasingMethod.InQuad:
					return f.InQuad();
				case EasingMethod.OutQuad:
					return f.OutQuad();
				case EasingMethod.InOutQuad:
					return f.InOutQuad();
				// Cubic
				case EasingMethod.InCubic:
					return f.InCubic();
				case EasingMethod.OutCubic:
					return f.OutCubic();
				case EasingMethod.InOutCubic:
					return f.InOutCubic();
				// Quart
				case EasingMethod.InQuart:
					return f.InQuart();
				case EasingMethod.OutQuart:
					return f.OutQuart();
				case EasingMethod.InOutQuart:
					return f.InOutQuart();
				// Quint
				case EasingMethod.InQuint:
					return f.InQuint();
				case EasingMethod.OutQuint:
					return f.OutQuint();
				case EasingMethod.InOutQuint:
					return f.InOutQuint();
				// Expo
				case EasingMethod.InExpo:
					return f.InExpo();
				case EasingMethod.OutExpo:
					return f.OutExpo();
				case EasingMethod.InOutExpo:
					return f.InOutExpo();
				// Circ
				case EasingMethod.InCirc:
					return f.InCirc();
				case EasingMethod.OutCirc:
					return f.OutCirc();
				case EasingMethod.InOutCirc:
					return f.InOutCirc();
				// Default
				case EasingMethod.Linear:
				default:
					return f;
			}
		}

		#region Sine

		/// <summary>
		/// First order ease. https://easings.net/#easeInSine
		/// </summary>
		public static float InSine(this float x)
		{
			return 1f - Mathf.Cos((x * Mathf.PI) / 2f);
		}

		/// <summary>
		/// First order ease. https://easings.net/#easeOutSine
		/// </summary>
		public static float OutSine(this float x)
		{
			return Mathf.Sin((x * Mathf.PI) / 2f);
		}

		/// <summary>
		/// First order ease. https://easings.net/#easeInOutSine
		/// </summary>
		public static float InOutSine(this float x)
		{
			return -(Mathf.Cos(Mathf.PI * x) - 1f) / 2f;
		}

		/// <summary>
		/// First order ease. https://easings.net/#easeInOutSine
		/// </summary>
		public static float ReverseInOutSine(this float x)
		{
			return InOutSine(1f - x);
		}

		#endregion Sine

		#region Quad

		/// <summary>
		/// Second order ease. https://easings.net/#easeInQuad
		/// </summary>
		public static float InQuad(this float x)
		{
			return x * x;
		}

		/// <summary>
		/// Second order ease. https://easings.net/#easeOutQuad
		/// </summary>
		public static float OutQuad(this float x)
		{
			return 1f - (1f - x) * (1f - x);
		}

		/// <summary>
		/// Second order ease. https://easings.net/#easeInOutQuad
		/// </summary>
		public static float InOutQuad(this float x)
		{
			return x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
		}

		#endregion Quad

		#region Cubic

		/// <summary>
		/// Third order ease. https://easings.net/#easeInCubic
		/// </summary>
		public static float InCubic(this float x)
		{
			return x * x * x;
		}

		/// <summary>
		/// Third order ease. https://easings.net/#easeOutCubic
		/// </summary>
		public static float OutCubic(this float x)
		{
			return 1f - Mathf.Pow(1f - x, 3f);
		}

		/// <summary>
		/// Third order ease. https://easings.net/#easeInOutCubic
		/// </summary>
		public static float InOutCubic(this float x)
		{
			return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
		}

		/// <summary>
		/// Third order ease. 1f - https://easings.net/#easeInOutCubic
		/// </summary>
		public static float ReverseInOutCubic(this float x)
		{
			return InOutCubic(1f - x);
		}

		#endregion Cubic

		#region Quart

		/// <summary>
		/// Fourth order ease. https://easings.net/#easeInQuart
		/// </summary>
		public static float InQuart(this float x)
		{
			return x * x * x * x;
		}

		/// <summary>
		/// Fourth order ease. https://easings.net/#easeOutQuart
		/// </summary>
		public static float OutQuart(this float x)
		{
			return 1f - Mathf.Pow(1f - x, 4f);
		}

		/// <summary>
		/// Fourth order ease. https://easings.net/#easeInOutQuart
		/// </summary>
		public static float InOutQuart(this float x)
		{
			return x < 0.5f ? 8f * x * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 4f) / 2f;
		}

		#endregion Quart

		#region Quint

		/// <summary>
		/// Fifth order ease. https://easings.net/#easeInQuint
		/// </summary>
		public static float InQuint(this float x)
		{
			return x * x * x * x * x;
		}

		/// <summary>
		/// Fifth order ease. https://easings.net/#easeOutQuint
		/// </summary>
		public static float OutQuint(this float x)
		{
			return 1f - Mathf.Pow(1f - x, 5f);
		}

		/// <summary>
		/// Fifth order ease. https://easings.net/#easeInOutQuint
		/// </summary>
		public static float InOutQuint(this float x)
		{
			return x < 0.5f ? 16f * x * x * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 5f) / 2f;
		}

		#endregion Quint

		#region Expo

		/// <summary>
		/// Sixth order ease. https://easings.net/#easeInExpo
		/// </summary>
		public static float InExpo(this float x)
		{
			return x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
		}

		/// <summary>
		/// Sixth order ease. https://easings.net/#easeOutExpo
		/// </summary>
		public static float OutExpo(this float x)
		{
			return x == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
		}

		/// <summary>
		/// Sixth order ease. https://easings.net/#easeInOutExpo
		/// </summary>
		public static float InOutExpo(this float x)
		{
			return x == 0f
				? 0
				: x == 1f
				? 1f
				: x < 0.5f ? Mathf.Pow(2f, 20f * x - 10f) / 2f
				: (2f - Mathf.Pow(2f, -20f * x + 10f)) / 2f;
		}

		#endregion Expo

		#region Circ

		/// <summary>
		/// https://easings.net/#easeInCirc
		/// </summary>
		public static float InCirc(this float x)
		{
			return 1f - Mathf.Sqrt(1f - Mathf.Pow(x, 2f));
		}

		/// <summary>
		/// https://easings.net/#easeOutCirc
		/// </summary>
		public static float OutCirc(this float x)
		{
			return Mathf.Sqrt(1f - Mathf.Pow(x - 1f, 2f));
		}

		/// <summary>
		/// https://easings.net/#easeInOutCirc
		/// </summary>
		public static float InOutCirc(this float x)
		{
			return x < 0.5f
				? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * x, 2f))) / 2f
				: (Mathf.Sqrt(1f - Mathf.Pow(-2f * x + 2f, 2f)) + 1f) / 2f;
		}

		#endregion Circ
	}
}
