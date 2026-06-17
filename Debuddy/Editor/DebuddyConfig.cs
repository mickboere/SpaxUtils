#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Debuddy
{
	/// <summary>
	/// Serializable settings for <see cref="DebuddyCore"/>, persisted as JSON at
	/// <c>&lt;project&gt;/Logs/Debuddy.config.json</c>.
	///
	/// This file IS the control surface: it can be edited by hand, by the
	/// <see cref="DebuddyWindow"/>, or by an external agent. The core hot-reloads it
	/// whenever the file changes on disk, so edits apply live (even during Play).
	/// </summary>
	[Serializable]
	public class DebuddyConfig
	{
		[Tooltip("Master switch. When false nothing is captured.")]
		public bool enabled = true;

		[Tooltip("If non-empty, only messages containing one of these (case-insensitive) substrings are captured. Empty = capture everything.")]
		public string[] includeKeywords = new string[0];

		[Tooltip("Messages containing any of these (case-insensitive) substrings are dropped, even if they matched an include keyword.")]
		public string[] excludeKeywords = new string[0];

		[Tooltip("Capture Log/Assert (informational) messages.")]
		public bool captureInfo = true;

		[Tooltip("Capture Warning messages.")]
		public bool captureWarnings = true;

		[Tooltip("Capture Error/Exception messages.")]
		public bool captureErrors = true;

		[Tooltip("Append the stack trace beneath each captured line. Off = message-only (small + readable).")]
		public bool includeStack = false;

		[Tooltip("Safety backstop: once the log reaches this size, capture stops (a marker is appended). 0 = unlimited. Config-only — not exposed in the window.")]
		public int maxFileSizeKB = 5120;

		public DebuddyConfig Clone()
		{
			DebuddyConfig c = (DebuddyConfig)MemberwiseClone();
			c.includeKeywords = includeKeywords != null ? (string[])includeKeywords.Clone() : new string[0];
			c.excludeKeywords = excludeKeywords != null ? (string[])excludeKeywords.Clone() : new string[0];
			return c;
		}

		/// <summary>Loads the config from disk, returning defaults if missing or unreadable.</summary>
		public static DebuddyConfig Load(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					string json = File.ReadAllText(path);
					if (!string.IsNullOrWhiteSpace(json))
					{
						DebuddyConfig c = JsonUtility.FromJson<DebuddyConfig>(json);
						if (c != null)
						{
							c.includeKeywords = c.includeKeywords ?? new string[0];
							c.excludeKeywords = c.excludeKeywords ?? new string[0];
							return c;
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Debuddy] Failed to load config '{path}': {e.Message}");
			}
			return new DebuddyConfig();
		}

		/// <summary>Writes the config to disk as pretty-printed JSON, creating the folder if needed.</summary>
		public void Save(string path)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, JsonUtility.ToJson(this, true));
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Debuddy] Failed to save config '{path}': {e.Message}");
			}
		}
	}
}
#endif
