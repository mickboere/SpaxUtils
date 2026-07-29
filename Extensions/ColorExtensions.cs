using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Collection of <see cref="Color"/> extension methods.
	/// </summary>
	public static class ColorExtensions
	{
		/// <summary>
		/// Inline <see cref="Color.Lerp(Color, Color, float)"/> extension.
		/// </summary>
		public static Color Lerp(this Color from, Color to, float t)
		{
			return Color.Lerp(from, to, t);
		}

		/// <summary>
		/// Sums the values of all channels.
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		public static float Sum(this Color c)
		{
			return c.r + c.g + c.b + c.a;
		}

		/// <summary>
		/// Normalizes the color so that the sum of all channels equals 1.
		/// </summary>
		public static Color Normalize(this Color c)
		{
			if (c == Color.clear)
			{
				return c;
			}
			return c / c.Sum();
		}

		public static float Luminance(this Color c)
		{
			return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
		}

		public static string RichWrap(this Color color, string wrap)
		{
			return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{wrap}</color>";
		}

		public static Color SetA(this Color color, float a)
		{
			color.a = a;
			return color;
		}

		/// <summary>
		/// Pulls <paramref name="color"/> towards white (positive <paramref name="shade"/>) or black (negative),
		/// leaving alpha untouched. Reaches both directions from one slider, unlike a tint multiply.
		/// </summary>
		public static Color Shade(this Color color, float shade)
		{
			Color target = shade >= 0f ? Color.white : Color.black;
			return Color.Lerp(color, target, Mathf.Abs(shade)).SetA(color.a);
		}
	}
}
