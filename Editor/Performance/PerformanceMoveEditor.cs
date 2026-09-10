using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Editing lens for a <see cref="PerformanceMove"/>. Non-timeline data sits in collapsible auto-hiding
	/// groups; the move's timeline is hosted inline so authoring never means hopping between assets.
	/// </summary>
	[CustomEditor(typeof(PerformanceMove), true)]
	public class PerformanceMoveEditor : Editor
	{
		#region Layout

		/// <summary>A serialized field, or something the editor draws itself. Either can hide.</summary>
		protected class FieldEntry
		{
			public string Path { get; }
			public Action Draw { get; }
			public Func<bool> Visible { get; }

			public FieldEntry(string path, Action draw, Func<bool> visible)
			{
				Path = path;
				Draw = draw;
				Visible = visible;
			}
		}

		/// <summary>A foldout of entries. A title of null draws its entries bare, without a header.</summary>
		protected class FieldGroup
		{
			public string Title { get; }
			public List<FieldEntry> Entries { get; } = new List<FieldEntry>();
			public List<string> Claimed { get; } = new List<string>();

			public FieldGroup(string title)
			{
				Title = title;
			}

			public FieldGroup Field(string path, Func<bool> visible = null)
			{
				Entries.Add(new FieldEntry(path, null, visible));
				return this;
			}

			public FieldGroup Custom(Action draw, Func<bool> visible = null)
			{
				Entries.Add(new FieldEntry(null, draw, visible));
				return this;
			}

			/// <summary>
			/// Marks fields a custom entry already draws. Without this they surface again under Other, which
			/// is otherwise the safety net for fields nobody claimed.
			/// </summary>
			public FieldGroup Claims(params string[] paths)
			{
				Claimed.AddRange(paths);
				return this;
			}
		}

		#endregion Layout

		private const string FOLD_PREF = "SpaxUtils.MoveEditor.";
		private const string SCRIPT_PROPERTY = "m_Script";
		private const string UNCLAIMED_TITLE = "Other";
		private const float SECONDS_WIDTH = 54f;

		protected PerformanceMove Move => (PerformanceMove)target;

		/// <summary>
		/// Created on demand and retargeted whenever the move's timeline changes, so a swapped asset never
		/// leaves the previous one's inspector on screen.
		/// </summary>
		private AnimationTimelineEditor TimelineEditor
		{
			get
			{
				// Explicit null branch: a DELETED asset reads as null while 'hosted' still references it, so
				// comparing the two would keep an editor pointed at nothing.
				if (Move.Timeline == null)
				{
					DestroyTimelineEditor();
					return null;
				}

				if (Move.Timeline != hosted)
				{
					DestroyTimelineEditor();
					hosted = Move.Timeline;
					timelineEditor = (AnimationTimelineEditor)CreateEditor(hosted, typeof(AnimationTimelineEditor));
				}

				if (timelineEditor != null)
				{
					timelineEditor.Suggestions = suggestions;
				}

				return timelineEditor;
			}
		}

		private static readonly GUIContent CHARGE_LABEL =
			new GUIContent("Has Charge", "Adds or removes the CHARGING region on the timeline.");
		private static readonly GUIContent PERFORMANCE_LABEL =
			new GUIContent("Has Performance", "Adds or removes the PERFORMING and FINISHING regions on the timeline.");

		private readonly Dictionary<string, bool> folds = new Dictionary<string, bool>();

		private List<FieldGroup> groups;
		private HashSet<string> claimed;
		private List<string> suggestions;
		private AnimationTimeline hosted;
		private AnimationTimelineEditor timelineEditor;
		private Action restructure;

		#region Lifecycle

		protected virtual void OnEnable()
		{
			suggestions = Move.MarkerSuggestions?.ToList();

			groups = new List<FieldGroup>();
			BuildLayout();
			claimed = new HashSet<string>(groups.SelectMany((g) => g.Claimed)
				.Concat(groups.SelectMany((g) => g.Entries).Where((e) => e.Path != null).Select((e) => e.Path)));
		}

		protected virtual void OnDisable()
		{
			DestroyTimelineEditor();
		}

		#endregion Lifecycle

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			foreach (FieldGroup group in groups)
			{
				DrawGroup(group);
			}

			DrawUnclaimed();

			serializedObject.ApplyModifiedProperties();
			ApplyRestructure();
		}

		#region Preview

		public override bool HasPreviewGUI()
		{
			return Move.UseTimeline && TimelineEditor != null && TimelineEditor.HasPreviewGUI();
		}

		public override GUIContent GetPreviewTitle()
		{
			return timelineEditor != null ? timelineEditor.GetPreviewTitle() : base.GetPreviewTitle();
		}

		public override void OnPreviewSettings()
		{
			timelineEditor?.OnPreviewSettings();
		}

		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
		{
			timelineEditor?.OnInteractivePreviewGUI(r, background);
		}

		#endregion Preview

		#region Layout building

		/// <summary>
		/// Declares the inspector. Entries naming a field this move type lacks simply do not appear, which is
		/// how one layout serves the whole hierarchy.
		/// </summary>
		protected virtual void BuildLayout()
		{
			Group(null)
				.Field("name")
				.Field("description")
				.Field("cancelDuration")
				.Field("blockSelfOverlap");

			// First, deliberately: the behaviours act the move out. Without them the rest is inert data.
			Group("Behaviour")
				.Field("behaviour")
				.Field("followUps");

			Group("Animation")
				.Field("animationType")
				.Field("animationIndex", () => Move.AnimationType == PerformanceAnimationType.Animator)
				.Field("posingData", () => Move.AnimationType == PerformanceAnimationType.Poser)
				.Custom(DrawTimelineLens, () => Move.AnimationType == PerformanceAnimationType.Timeline)
				.Claims(TimelineOwnership.TIMELINE_FIELD);

			Group("Charging")
				.Custom(DrawChargeToggle)
				.Claims("hasCharge")
				.Field("chargeDuration", () => Move.HasCharge && !ChargeIsAnimated)
				.Custom(() => Readout("Charge Duration", Move.ChargeDuration), () => Move.HasCharge && ChargeIsAnimated)
				.Custom(DrawMinCharge, () => Move.HasCharge)
				.Claims("minCharge")
				.Field("requireMinCharge", () => Move.HasCharge)
				.Field("chargeSpeedMultiplier", () => Move.HasCharge)
				.Field("chargeCost", () => Move.HasCharge);

			Group("Performance")
				.Custom(DrawPerformanceToggle)
				.Claims("hasPerformance")
				.Field("minDuration", () => Move.HasPerformance && !Move.UseTimeline)
				.Custom(() => Readout("Min Duration", Move.MinDuration), () => Move.HasPerformance && Move.UseTimeline)
				.Field("release", () => !Move.UseTimeline)
				.Custom(() => Readout("Release", Move.Release), () => Move.UseTimeline)
				.Field("chargeFadeout", () => Move.HasPerformance && Move.AnimationType == PerformanceAnimationType.Poser)
				.Field("performSpeedMultiplier", () => Move.HasPerformance)
				.Field("performCost", () => Move.HasPerformance);

			Group("Combat")
				.Field("range");

			BuildAdditionalGroups();
		}

		/// <summary>Override point for move-type groups; they land after the core groups.</summary>
		protected virtual void BuildAdditionalGroups()
		{
		}

		protected FieldGroup Group(string title)
		{
			FieldGroup group = new FieldGroup(title);
			groups.Add(group);
			return group;
		}

		#endregion Layout building

		#region Drawing

		/// <summary>Whether a CHARGING region with real length is supplying the charge's duration.</summary>
		private bool ChargeIsAnimated
		{
			get
			{
				return Move.UseTimeline &&
					Move.Timeline.TryGetMarker(TimelineMarkerIdentifiers.CHARGING, out ResolvedMarker charging) &&
					charging.Length > 0f;
			}
		}

		/// <summary>
		/// A fraction is unreadable on its own, so the seconds it produces sit next to it. Charge is bounded
		/// by its own maximum, which is why this is a slider rather than a duration.
		/// </summary>
		private void DrawMinCharge()
		{
			Rect rect = EditorGUILayout.GetControlRect();
			Rect field = new Rect(rect.x, rect.y, rect.width - SECONDS_WIDTH, rect.height);
			EditorGUI.PropertyField(field, serializedObject.FindProperty("minCharge"));

			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUI.LabelField(new Rect(field.xMax + 4f, rect.y, SECONDS_WIDTH, rect.height),
					$"{Move.MinCharge:0.000}s", EditorStyles.miniLabel);
			}
		}

		/// <summary>Draws a value the timeline supplies, standing in for the field it replaced.</summary>
		protected static void Readout(string label, float seconds)
		{
			using (new EditorGUI.DisabledScope(true))
			{
				EditorGUILayout.LabelField(label, $"{seconds:0.000}s   (markers)");
			}
		}

		private void DrawGroup(FieldGroup group)
		{
			List<FieldEntry> visible = group.Entries.Where(IsVisible).ToList();
			if (visible.Count == 0)
			{
				return;
			}

			if (group.Title == null)
			{
				DrawEntries(visible);
				return;
			}

			if (!DrawHeader(group.Title))
			{
				return;
			}

			EditorGUI.indentLevel++;
			DrawEntries(visible);
			EditorGUI.indentLevel--;
		}

		/// <summary>
		/// A plain Foldout wearing the header style, NOT BeginFoldoutHeaderGroup: these groups host whole
		/// nested inspectors, and a Begin/End pair left unbalanced in there latches for every later group.
		/// </summary>
		private bool DrawHeader(string title)
		{
			EditorGUILayout.Space(2);
			bool open = EditorGUILayout.Foldout(Foldout(title), title, true, EditorStyles.foldoutHeader);
			SetFoldout(title, open);
			return open;
		}

		private void DrawEntries(List<FieldEntry> entries)
		{
			foreach (FieldEntry entry in entries)
			{
				if (entry.Draw != null)
				{
					entry.Draw();
					continue;
				}

				EditorGUILayout.PropertyField(serializedObject.FindProperty(entry.Path), true);
			}
		}

		/// <summary>
		/// Everything the layout does not mention, so a field added later surfaces here instead of silently
		/// becoming uneditable.
		/// </summary>
		private void DrawUnclaimed()
		{
			List<string> unclaimed = new List<string>();
			SerializedProperty iterator = serializedObject.GetIterator();

			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;
				if (iterator.propertyPath != SCRIPT_PROPERTY && !claimed.Contains(iterator.propertyPath))
				{
					unclaimed.Add(iterator.propertyPath);
				}
			}

			if (unclaimed.Count == 0)
			{
				return;
			}

			if (!DrawHeader(UNCLAIMED_TITLE))
			{
				return;
			}

			EditorGUI.indentLevel++;
			foreach (string path in unclaimed)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty(path), true);
			}
			EditorGUI.indentLevel--;
		}

		private bool IsVisible(FieldEntry entry)
		{
			if (entry.Visible != null && !entry.Visible())
			{
				return false;
			}

			return entry.Path == null || serializedObject.FindProperty(entry.Path) != null;
		}

		#endregion Drawing

		#region Timeline lens

		/// <summary>Ownership row plus the timeline's own inspector, hosted inline.</summary>
		private void DrawTimelineLens()
		{
			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(serializedObject.FindProperty(TimelineOwnership.TIMELINE_FIELD));
			if (EditorGUI.EndChangeCheck())
			{
				// Apply at once so the hosted inspector below retargets in this pass rather than the next.
				serializedObject.ApplyModifiedProperties();
			}

			DrawOwnership();

			AnimationTimelineEditor editor = TimelineEditor;
			if (editor != null)
			{
				using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
				{
					editor.OnInspectorGUI();
				}
			}

			EditorGUI.indentLevel = indent;
		}

		private void DrawOwnership()
		{
			AnimationTimeline timeline = Move.Timeline;
			PerformanceMove move = Move;

			using (new EditorGUILayout.HorizontalScope())
			{
				if (timeline == null)
				{
					EditorGUILayout.LabelField("No timeline yet.", EditorStyles.miniLabel);
					if (GUILayout.Button("Create", GUILayout.Width(90f)))
					{
						restructure = () => TimelineOwnership.Create(move);
					}
					return;
				}

				bool owned = TimelineOwnership.IsOwned(move, timeline);
				EditorGUILayout.LabelField(owned ? "Child asset of this move" : "Shared standalone asset",
					EditorStyles.miniLabel);

				if (owned)
				{
					if (GUILayout.Button("Extract", GUILayout.Width(80f)))
					{
						restructure = () => TimelineOwnership.Extract(move, timeline);
					}
					if (GUILayout.Button("Delete", GUILayout.Width(60f)))
					{
						restructure = () => TimelineOwnership.Delete(move, timeline);
					}
					return;
				}

				if (GUILayout.Button("Embed Copy", GUILayout.Width(90f)))
				{
					restructure = () => TimelineOwnership.Embed(move, timeline);
				}

				// Only unassigns. A shared asset outlives any one move's reference to it.
				if (GUILayout.Button("Clear", GUILayout.Width(60f)))
				{
					restructure = () => TimelineOwnership.Assign(move, null);
				}
			}
		}

		/// <summary>
		/// Asset-structure changes run after the pass that asked for them. Doing it inline would leave the
		/// remainder of the pass drawing against a layout that no longer matches.
		/// </summary>
		private void ApplyRestructure()
		{
			if (restructure == null)
			{
				return;
			}

			Action action = restructure;
			restructure = null;
			action();

			serializedObject.Update();
			Repaint();
		}

		private void DestroyTimelineEditor()
		{
			if (timelineEditor != null)
			{
				DestroyImmediate(timelineEditor);
				timelineEditor = null;
			}

			hosted = null;
		}

		#endregion Timeline lens

		#region Phase toggles

		/// <summary>
		/// Under a timeline the CHARGING region IS the flag, so the checkbox adds or removes it rather than
		/// writing a bool. Two sources of truth is precisely the desync the derived flags remove.
		/// </summary>
		private void DrawChargeToggle()
		{
			DrawPhaseToggle(CHARGE_LABEL, "hasCharge", Move.HasCharge, (timelineObject, present) =>
			{
				if (present)
				{
					TimelineAuthoring.AddPhase(timelineObject, Move.Timeline,
						TimelineMarkerIdentifiers.CHARGING, TimelineMarkerIdentifiers.PERFORMING, 0f);
					return;
				}

				TimelineAuthoring.RemoveMarkers(timelineObject, TimelineMarkerIdentifiers.CHARGING);
			});
		}

		/// <summary>
		/// PERFORMING and FINISHING are the swing's opening and closing brace, so the toggle carries both.
		/// A stranded FINISHING would otherwise keep supplying a Release for a move that never performs.
		/// </summary>
		private void DrawPerformanceToggle()
		{
			DrawPhaseToggle(PERFORMANCE_LABEL, "hasPerformance", Move.HasPerformance, (timelineObject, present) =>
			{
				if (present)
				{
					TimelineAuthoring.AddPhase(timelineObject, Move.Timeline,
						TimelineMarkerIdentifiers.PERFORMING, TimelineMarkerIdentifiers.FINISHING, 0f);
					return;
				}

				TimelineAuthoring.RemoveMarkers(timelineObject, TimelineMarkerIdentifiers.PERFORMING);
				TimelineAuthoring.RemoveMarkers(timelineObject, TimelineMarkerIdentifiers.FINISHING);
			});
		}

		private void DrawPhaseToggle(GUIContent label, string fallbackField, bool current,
			Action<SerializedObject, bool> apply)
		{
			if (!Move.UseTimeline)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty(fallbackField));
				return;
			}

			bool present = EditorGUILayout.Toggle(label, current);
			if (present == current)
			{
				return;
			}

			// Its own SerializedObject: the hosted inspector holds another, and sharing one would let
			// whichever applies last overwrite the other.
			SerializedObject timelineObject = new SerializedObject(Move.Timeline);
			apply(timelineObject, present);
			timelineObject.ApplyModifiedProperties();
			Move.Timeline.Resolve();
			Repaint();
		}

		#endregion Phase toggles

		#region Foldout state

		/// <summary>Read from EditorPrefs once, so a repaint does not write to disk on every frame.</summary>
		private bool Foldout(string title)
		{
			if (!folds.TryGetValue(title, out bool open))
			{
				open = EditorPrefs.GetBool(FoldKey(title), true);
				folds[title] = open;
			}

			return open;
		}

		private void SetFoldout(string title, bool open)
		{
			if (Foldout(title) == open)
			{
				return;
			}

			folds[title] = open;
			EditorPrefs.SetBool(FoldKey(title), open);
		}

		private string FoldKey(string title)
		{
			return FOLD_PREF + target.GetType().Name + "." + title;
		}

		#endregion Foldout state
	}
}
