using UnityEngine;

namespace SpaxUtils
{
	public static class DebugUtil
	{
		public static string ColorFloat(float f, Gradient gradient)
		{
			return $"<color=#{ColorUtility.ToHtmlStringRGBA(gradient.Evaluate(f))}>{f:N2}</color>";
		}

		public static string ColorBool(bool b, Gradient gradient)
		{
			return $"<color=#{ColorUtility.ToHtmlStringRGBA(gradient.Evaluate(b ? 1f : 0f))}>{b}</color>";
		}
	}
}
