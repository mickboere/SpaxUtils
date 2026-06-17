#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Debuddy
{
	/// <summary>
	/// File-backed, session-scoped, filtered log sink for the Unity Editor.
	///
	/// Hooks <see cref="Application.logMessageReceivedThreaded"/> and writes the matching
	/// lines to <c>&lt;project&gt;/Logs/Debuddy.log</c> (outside Assets/ so no reimport,
	/// gitignored so no noise). Cleared on entering Play so each session starts fresh.
	///
	/// Everything is file-driven: settings live in <c>Debuddy.config.json</c> and are
	/// hot-reloaded on change, output lives in <c>Debuddy.log</c>. The
	/// <see cref="DebuddyWindow"/> is just a human-facing mirror of that same state.
	/// </summary>
	[InitializeOnLoad]
	public static class DebuddyCore
	{
		public class Entry
		{
			public string message;
			public string stackTrace;
			public LogType type;
			public float time;
			public int frame;
		}

		// --- Paths (cached on the main thread at load; Application.dataPath is main-thread only) ---
		public static string LogsDir { get; private set; }
		public static string LogFilePath { get; private set; }
		public static string ConfigFilePath { get; private set; }

		// --- Config (reference-swapped on reload so the threaded callback reads it lock-free) ---
		private static volatile DebuddyConfig config;
		private static DateTime configWriteTimeUtc;
		private static double nextConfigCheck;
		public static DebuddyConfig CurrentConfig => config ?? (config = DebuddyConfig.Load(ConfigFilePath));

		// --- File writer ---
		private static readonly object fileLock = new object();
		private static long currentSize;
		private static volatile bool capReached;

		// --- Frame/time cache (refreshed on the main thread, read from off-thread callbacks) ---
		// Play-mode Time.frameCount / realtimeSinceStartup already reset to 0 when entering
		// Play, so these are session-relative as-is — no baseline offset needed. Main-thread
		// logs read Time directly (exact, matches the engine); the cache is the off-thread fallback.
		private static volatile int cachedFrame;
		private static float cachedTime;
		private static int mainThreadId;

		// --- In-memory ring buffer for the window ---
		private const int MaxEntries = 2000;
		private static readonly object entriesLock = new object();
		private static readonly Queue<Entry> entries = new Queue<Entry>();
		private static int version;
		/// <summary>Increments on every captured entry / clear — the window polls this to know when to repaint.</summary>
		public static int Version => version;

		static DebuddyCore()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId; // InitializeOnLoad runs on the main thread
			LogsDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
			LogFilePath = Path.Combine(LogsDir, "Debuddy.log");
			ConfigFilePath = Path.Combine(LogsDir, "Debuddy.config.json");

			EnsureConfig();
			try { currentSize = File.Exists(LogFilePath) ? new FileInfo(LogFilePath).Length : 0; }
			catch { currentSize = 0; }

			Application.logMessageReceivedThreaded += OnLog;
			EditorApplication.update += OnEditorUpdate;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		// ---------------------------------------------------------------- public API

		/// <summary>Returns a snapshot of the captured entries (newest last).</summary>
		public static Entry[] GetEntries()
		{
			lock (entriesLock) { return entries.ToArray(); }
		}

		/// <summary>Persists the given config to disk and applies it immediately.</summary>
		public static void SaveConfig(DebuddyConfig newConfig)
		{
			if (newConfig == null) { return; }
			newConfig.Save(ConfigFilePath);
			config = newConfig;
			try { configWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigFilePath); }
			catch { /* ignore */ }
		}

		/// <summary>Empties the log file and the in-memory buffer (a new session).</summary>
		public static void ClearLog()
		{
			lock (fileLock)
			{
				try
				{
					Directory.CreateDirectory(LogsDir);
					File.WriteAllText(LogFilePath, string.Empty);
					currentSize = 0;
					capReached = false;
				}
				catch (Exception e) { Debug.LogWarning($"[Debuddy] Failed to clear log: {e.Message}"); }
			}
			lock (entriesLock) { entries.Clear(); }
			Interlocked.Increment(ref version);
		}

		// ---------------------------------------------------------------- log handling

		private static void OnLog(string condition, string stackTrace, LogType type)
		{
			DebuddyConfig c = config;
			if (c == null || !c.enabled) { return; }
			if (capReached) { return; } // size backstop tripped — stop collecting until next session
			if (!PassesTypeFilter(c, type)) { return; }
			if (!PassesKeywords(c, condition)) { return; }

			// Main-thread logs fire synchronously inside Debug.Log, so Time is exact here and
			// matches the engine's own frame number; off-thread logs use the cached values.
			bool onMainThread = Thread.CurrentThread.ManagedThreadId == mainThreadId;
			int frame = onMainThread ? Time.frameCount : cachedFrame;
			float time = onMainThread ? Time.realtimeSinceStartup : cachedTime;

			// Only retain the stack when asked — keeps the window detail pane consistent with the file.
			Enqueue(new Entry { message = condition, stackTrace = c.includeStack ? stackTrace : null, type = type, time = time, frame = frame });

			// Fixed-width prefix so messages line up in any monospace viewer.
			StringBuilder sb = new StringBuilder();
			sb.Append('[').Append(time.ToString("0.00").PadLeft(7)).Append("s f").Append(frame.ToString().PadLeft(6)).Append("] ").Append(condition);
			if (c.includeStack && !string.IsNullOrEmpty(stackTrace))
			{
				sb.Append('\n').Append(stackTrace.TrimEnd());
			}
			WriteLine(sb.ToString(), c);
		}

		private static bool PassesTypeFilter(DebuddyConfig c, LogType type)
		{
			switch (type)
			{
				case LogType.Warning: return c.captureWarnings;
				case LogType.Error:
				case LogType.Exception:
				case LogType.Assert: return c.captureErrors;
				default: return c.captureInfo; // LogType.Log
			}
		}

		private static bool PassesKeywords(DebuddyConfig c, string message)
		{
			message = message ?? string.Empty;

			if (c.excludeKeywords != null)
			{
				foreach (string ex in c.excludeKeywords)
				{
					if (!string.IsNullOrEmpty(ex) && message.IndexOf(ex, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return false;
					}
				}
			}

			if (c.includeKeywords == null || c.includeKeywords.Length == 0)
			{
				return true; // no include filter = capture everything
			}

			foreach (string inc in c.includeKeywords)
			{
				if (!string.IsNullOrEmpty(inc) && message.IndexOf(inc, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private static void WriteLine(string text, DebuddyConfig c)
		{
			lock (fileLock)
			{
				try
				{
					// Backstop: once over the cap, append a one-time marker and stop. Existing
					// log data is preserved; we just stop growing (resets next session on Clear).
					if (c.maxFileSizeKB > 0 && currentSize > (long)c.maxFileSizeKB * 1024L)
					{
						string marker = $"[Debuddy] size cap of {c.maxFileSizeKB} KB reached\n";
						File.AppendAllText(LogFilePath, marker);
						currentSize += Encoding.UTF8.GetByteCount(marker);
						capReached = true;
						return;
					}

					string line = text + "\n";
					File.AppendAllText(LogFilePath, line);
					currentSize += Encoding.UTF8.GetByteCount(line);
				}
				catch
				{
					// Swallow — logging about a logging failure would recurse / spam the console.
				}
			}
		}

		private static void Enqueue(Entry e)
		{
			lock (entriesLock)
			{
				entries.Enqueue(e);
				while (entries.Count > MaxEntries) { entries.Dequeue(); }
			}
			Interlocked.Increment(ref version);
		}

		// ---------------------------------------------------------------- editor loop

		private static void OnEditorUpdate()
		{
			cachedFrame = Time.frameCount;
			cachedTime = Time.realtimeSinceStartup;

			if (EditorApplication.timeSinceStartup >= nextConfigCheck)
			{
				nextConfigCheck = EditorApplication.timeSinceStartup + 0.5;
				ReloadConfigIfChanged();
			}
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			// Clear on ExitingEditMode (before scene Awake/OnEnable) so the session's very
			// first frame-0 logs are kept. EnteredPlayMode fires *after* Awake, so clearing
			// there would wipe them.
			if (change == PlayModeStateChange.ExitingEditMode)
			{
				ClearLog(); // session-scoped: every Play starts fresh
			}
		}

		// ---------------------------------------------------------------- config persistence

		private static void EnsureConfig()
		{
			try { Directory.CreateDirectory(LogsDir); } catch { /* ignore */ }

			if (File.Exists(ConfigFilePath))
			{
				ReloadConfig();
			}
			else
			{
				config = new DebuddyConfig();
				config.Save(ConfigFilePath);
				try { configWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigFilePath); }
				catch { /* ignore */ }
			}
		}

		private static void ReloadConfigIfChanged()
		{
			try
			{
				if (!File.Exists(ConfigFilePath)) { return; }
				DateTime mt = File.GetLastWriteTimeUtc(ConfigFilePath);
				if (mt != configWriteTimeUtc)
				{
					ReloadConfig();
				}
			}
			catch { /* ignore */ }
		}

		private static void ReloadConfig()
		{
			config = DebuddyConfig.Load(ConfigFilePath);
			try { configWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigFilePath); }
			catch { /* ignore */ }
		}
	}
}
#endif
