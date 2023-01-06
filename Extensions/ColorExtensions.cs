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

			//return new Color(
			//	Mathf.Lerp(from.r, to.r, t),
			//	Mathf.Lerp(from.g, to.g, t),
			//	Mathf.Lerp(from.b, to.b, t),
			//	Mathf.Lerp(from.a, to.a, t));
		}

		public static float Sum(this Color c)
		{
			return c.r + c.g + c.b + c.a;
		}

		public static string RichWrap(this Color color, string wrap)
		{
			return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{wrap}</color>";
		}
	}
}
