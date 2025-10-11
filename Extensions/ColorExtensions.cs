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

		public static string RichWrap(this Color color, string wrap)
		{
			return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{wrap}</color>";
		}

		public static Color SetA(this Color color, float a)
		{
			color.a = a;
			return color;
		}
	}
}
