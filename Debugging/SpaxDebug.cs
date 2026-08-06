using UnityEngine;
using System.Diagnostics;
using System;
using Object = UnityEngine.Object;

namespace SpaxUtils
{
	/// <summary>
	/// Class that handles fancy logs.
	/// </summary>
	public static class SpaxDebug
	{
		public static bool Debugging { get; set; }

		public static void Log(
			string coloredText, string nonColoredText = "",
			LogType logType = LogType.Log, Color? color = null,
			Object context = null, int callerIndex = 1, string callerOverride = "")
		{
			if (Debugging || logType != LogType.Notify)
			{
				string caller = callerOverride;
				if (string.IsNullOrEmpty(callerOverride))
				{
					StackFrame frame = new StackTrace().GetFrame(callerIndex);
					if (frame != null)
					{
						caller = frame.GetMethod().ReflectedType.Name;
					}
					else
					{
						throw new IndexOutOfRangeException("StackTrace frame out of range.");
					}
				}

#if UNITY_EDITOR
				string textColor = color.HasValue ? textColor = ColorUtility.ToHtmlStringRGB(color.Value) :
					(UnityEditor.EditorGUIUtility.isProSkin ? ColorUtility.ToHtmlStringRGB(Color.white) : ColorUtility.ToHtmlStringRGB(Color.black));
				//string log = $"<color=#acd1e3>[{Time.frameCount}] <b>[{caller}]</b></color> <color=#{c}>{coloredText}</color>{nonColoredText}\n";
				// Full value: these sit on the console's dark background, where anything dimmed is unreadable.
				string signatureColor = caller.ToColorHex();
				string log = $"[{Time.frameCount}] <color=#{signatureColor}><b>[{caller}]</b></color> <color=#{textColor}>{coloredText}</color> {nonColoredText}\n";
#else
				string log = $"[{caller}]({Time.frameCount}) {coloredText}{nonColoredText}";
#endif

				switch (logType)
				{
					case LogType.Notify:
					case LogType.Log:
						UnityEngine.Debug.Log(log, context);
						break;
					case LogType.Warning:
						//UnityEngine.Debug.LogWarning(log, context);
						UnityEngine.Debug.LogWarning(log, context);
						break;
					case LogType.Error:
						UnityEngine.Debug.LogError(log, context);
						break;
				}
			}
		}

		public static void Notify(string header, string body = "", Object context = null)
		{
			Log(header, " " + body, LogType.Notify, null, context, 2);
		}

		public static void Warning(string header, string body = "", Object context = null)
		{
			Log(header, " " + body, LogType.Warning, Color.yellow, context, 2);
		}

		public static void Error(string header, string body = "", Object context = null)
		{
			Log(header, " " + body, LogType.Error, Color.red, context, 2);
		}

	}
}
