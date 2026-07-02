#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Debuddy
{
	/// <summary>
	/// Console-style human view over <see cref="DebuddyCore"/>: a toolbar of capture
	/// options up top and the collected log list below, with a detail pane for the selection.
	///
	/// Every control reads/writes the same <c>Debuddy.config.json</c> the core uses, so it
	/// stays in sync with edits made elsewhere (e.g. by an agent editing the file directly).
	/// </summary>
	public class DebuddyWindow : EditorWindow
	{
		private Vector2 listScroll;
		private Vector2 detailScroll;
		private DebuddyCore.Entry selected;
		private string includeText = string.Empty;
		private string excludeText = string.Empty;
		private string search = string.Empty;
		private int lastVersion = -1;
		private float detailHeight = 150f;
		private bool draggingSplitter;

		// Adjustable column for the [time/frame] prefix so all messages start at the same x.
		// Drag the divider in the column header to resize; persisted across sessions.
		private float prefixColumnWidth = 88f;
		private bool draggingColumn;
		private const string PrefixWidthPrefKey = "Debuddy.PrefixColumnWidth";

		// Virtualized list: cache the filtered snapshot and only redraw the visible rows,
		// so cost is constant regardless of how many entries are buffered.
		private readonly List<DebuddyCore.Entry> filtered = new List<DebuddyCore.Entry>();
		private DebuddyCore.Entry[] snapshot = new DebuddyCore.Entry[0];
		private int filteredVersion = -1;
		private string lastSearch = null;
		private const float RowHeight = 18f;

		// Per-type counts of the buffered entries, shown on the toolbar toggles (like the Console).
		private int infoCount, warnCount, errorCount;

		private GUIContent infoIcon, warnIcon, errorIcon;

		[MenuItem("Tools/Debuddy/Log Capture")]
		public static void Open()
		{
			DebuddyWindow w = GetWindow<DebuddyWindow>();
			w.titleContent = new GUIContent("Debuddy");
			w.minSize = new Vector2(380, 260);
			w.Show();
		}

		private void OnEnable()
		{
			infoIcon = EditorGUIUtility.IconContent("console.infoicon.sml");
			warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
			errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
			prefixColumnWidth = EditorPrefs.GetFloat(PrefixWidthPrefKey, 88f);
			PullKeywordsFromConfig();
		}

		private void OnDisable()
		{
			EditorPrefs.SetFloat(PrefixWidthPrefKey, prefixColumnWidth);
		}

		private void OnInspectorUpdate()
		{
			// Repaint when new entries arrive, and re-pull config edits made elsewhere
			// (only while the user isn't typing into our fields).
			if (DebuddyCore.Version != lastVersion)
			{
				lastVersion = DebuddyCore.Version;
				Repaint();
			}
			if (GUIUtility.keyboardControl == 0)
			{
				PullKeywordsFromConfig();
			}
		}

		private void PullKeywordsFromConfig()
		{
			DebuddyConfig c = DebuddyCore.CurrentConfig;
			includeText = string.Join(", ", c.includeKeywords ?? new string[0]);
			excludeText = string.Join(", ", c.excludeKeywords ?? new string[0]);
		}

		private static string[] ParseKeywords(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) { return new string[0]; }
			return text.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => s.Length > 0)
				.ToArray();
		}

		private void OnGUI()
		{
			DebuddyConfig current = DebuddyCore.CurrentConfig;
			DebuddyConfig edited = current.Clone();

			DrawToolbar(edited);
			DrawFilters(edited);

			// Persist if anything changed this frame.
			if (JsonUtility.ToJson(edited) != JsonUtility.ToJson(current))
			{
				DebuddyCore.SaveConfig(edited);
			}

			DrawList(edited);
			DrawDetail();
		}

		private void DrawToolbar(DebuddyConfig edited)
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			edited.enabled = GUILayout.Toggle(edited.enabled, edited.enabled ? "● Capturing" : "○ Paused", EditorStyles.toolbarButton, GUILayout.Width(90));

			if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48)))
			{
				DebuddyCore.ClearLog();
				selected = null;
			}

			GUILayout.Space(8);
			edited.captureInfo = GUILayout.Toggle(edited.captureInfo, new GUIContent(" " + infoCount, infoIcon.image), EditorStyles.toolbarButton, GUILayout.Width(52));
			edited.captureWarnings = GUILayout.Toggle(edited.captureWarnings, new GUIContent(" " + warnCount, warnIcon.image), EditorStyles.toolbarButton, GUILayout.Width(52));
			edited.captureErrors = GUILayout.Toggle(edited.captureErrors, new GUIContent(" " + errorCount, errorIcon.image), EditorStyles.toolbarButton, GUILayout.Width(52));

			GUILayout.FlexibleSpace();

			edited.includeStack = GUILayout.Toggle(edited.includeStack, "Stack", EditorStyles.toolbarButton, GUILayout.Width(48));
			if (GUILayout.Button("Reveal File", EditorStyles.toolbarButton, GUILayout.Width(80)))
			{
				EditorUtility.RevealInFinder(DebuddyCore.LogFilePath);
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawFilters(DebuddyConfig edited)
		{
			float prev = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 58;

			EditorGUILayout.BeginHorizontal();
			includeText = EditorGUILayout.TextField("Include", includeText);
			excludeText = EditorGUILayout.TextField("Exclude", excludeText);
			EditorGUILayout.EndHorizontal();
			edited.includeKeywords = ParseKeywords(includeText);
			edited.excludeKeywords = ParseKeywords(excludeText);

			search = EditorGUILayout.TextField("Search", search);

			EditorGUIUtility.labelWidth = prev;
		}

		private void DrawList(DebuddyConfig edited)
		{
			DrawColumnHeader();
			RefreshFilteredIfNeeded();

			Rect viewRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			float contentHeight = filtered.Count * RowHeight;
			Rect content = new Rect(0f, 0f, viewRect.width - 16f, contentHeight);

			listScroll = GUI.BeginScrollView(viewRect, listScroll, content);

			// Only iterate the rows currently on screen.
			int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / RowHeight));
			int last = Mathf.Min(filtered.Count, first + Mathf.CeilToInt(viewRect.height / RowHeight) + 1);

			Event ev = Event.current;
			for (int i = first; i < last; i++)
			{
				DebuddyCore.Entry e = filtered[i];
				Rect row = new Rect(0f, i * RowHeight, content.width, RowHeight);

				if (ReferenceEquals(e, selected))
				{
					EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.90f, 0.35f));
				}
				else if ((i & 1) == 1)
				{
					EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
				}

				if (ev.type == EventType.MouseDown && row.Contains(ev.mousePosition))
				{
					selected = e;
					ev.Use();
					Repaint();
				}

				Rect iconR = new Rect(row.x + 2f, row.y, 18f, RowHeight);
				Rect prefixR = new Rect(iconR.xMax + 2f, row.y, prefixColumnWidth, RowHeight);
				Rect msgR = new Rect(prefixR.xMax + 4f, row.y, Mathf.Max(0f, content.width - prefixR.xMax - 4f), RowHeight);

				GUI.Label(iconR, IconFor(e.type));
				GUI.Label(prefixR, $"<color=#888888>[{e.time:0.00}s f{e.frame}]</color>", RichLabel);
				GUI.Label(msgR, FirstLine(e.message), RichLabel);
			}

			GUI.EndScrollView();
		}

		private void RefreshFilteredIfNeeded()
		{
			int v = DebuddyCore.Version;
			if (v == filteredVersion && search == lastSearch) { return; }
			filteredVersion = v;
			lastSearch = search;

			snapshot = DebuddyCore.GetEntries();
			filtered.Clear();
			infoCount = warnCount = errorCount = 0;
			bool hasSearch = !string.IsNullOrEmpty(search);
			for (int i = 0; i < snapshot.Length; i++)
			{
				DebuddyCore.Entry e = snapshot[i];

				switch (e.type)
				{
					case LogType.Warning: warnCount++; break;
					case LogType.Error:
					case LogType.Exception:
					case LogType.Assert: errorCount++; break;
					default: infoCount++; break;
				}

				if (hasSearch && (e.message == null || e.message.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0))
				{
					continue;
				}
				filtered.Add(e);
			}
		}

		private void DrawDetail()
		{
			if (selected == null) { return; }

			// Draggable splitter so the pane can be resized.
			Rect handle = GUILayoutUtility.GetRect(0f, 5f, GUILayout.ExpandWidth(true));
			EditorGUI.DrawRect(handle, new Color(0f, 0f, 0f, 0.35f));
			EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeVertical);
			HandleSplitterDrag(handle);

			string text = selected.message ?? string.Empty;
			if (!string.IsNullOrEmpty(selected.stackTrace))
			{
				text += "\n\n" + selected.stackTrace;
			}

			// Compute the full content height so the scroll view actually scrolls.
			float contentWidth = Mathf.Max(50f, position.width - 22f);
			float contentHeight = RichWrap.CalcHeight(new GUIContent(text), contentWidth);

			detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.Height(detailHeight));
			EditorGUILayout.SelectableLabel(text, RichWrap, GUILayout.Width(contentWidth), GUILayout.Height(contentHeight));
			EditorGUILayout.EndScrollView();
		}

		private void DrawColumnHeader()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Space(20f);
			GUILayout.Label("time / frame", EditorStyles.miniLabel, GUILayout.Width(prefixColumnWidth));
			Rect divider = GUILayoutUtility.GetRect(5f, 5f, GUILayout.Width(5f), GUILayout.ExpandHeight(true));
			EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.35f));
			EditorGUIUtility.AddCursorRect(divider, MouseCursor.ResizeHorizontal);
			HandleColumnDrag(divider);
			GUILayout.Label("message", EditorStyles.miniLabel);
			EditorGUILayout.EndHorizontal();
		}

		private void HandleColumnDrag(Rect handle)
		{
			Event e = Event.current;
			if (e.type == EventType.MouseDown && handle.Contains(e.mousePosition))
			{
				draggingColumn = true;
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && draggingColumn)
			{
				prefixColumnWidth = Mathf.Clamp(prefixColumnWidth + e.delta.x, 40f, 400f);
				Repaint();
				e.Use();
			}
			else if (e.type == EventType.MouseUp && draggingColumn)
			{
				draggingColumn = false;
				EditorPrefs.SetFloat(PrefixWidthPrefKey, prefixColumnWidth);
			}
		}

		private void HandleSplitterDrag(Rect handle)
		{
			Event e = Event.current;
			if (e.type == EventType.MouseDown && handle.Contains(e.mousePosition))
			{
				draggingSplitter = true;
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && draggingSplitter)
			{
				detailHeight = Mathf.Clamp(detailHeight - e.delta.y, 48f, position.height - 120f);
				Repaint();
				e.Use();
			}
			else if (e.type == EventType.MouseUp)
			{
				draggingSplitter = false;
			}
		}

		private GUIContent IconFor(LogType type)
		{
			switch (type)
			{
				case LogType.Warning: return warnIcon;
				case LogType.Error:
				case LogType.Exception:
				case LogType.Assert: return errorIcon;
				default: return infoIcon;
			}
		}

		private static string FirstLine(string s)
		{
			if (string.IsNullOrEmpty(s)) { return string.Empty; }
			int nl = s.IndexOf('\n');
			return nl < 0 ? s : s.Substring(0, nl);
		}

		// --- text styles (rich text so <color>/<b> tags render like the Console) ---
		private static GUIStyle richLabel, richWrap;
		private static GUIStyle RichLabel => richLabel ?? (richLabel = new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip });
		private static GUIStyle RichWrap => richWrap ?? (richWrap = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });
	}
}
#endif
