using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws an <see cref="AnimationTimeline"/> as a ruler with draggable markers and regions.
	/// Ends are resolved through the runtime path so the view can never disagree with playback.
	/// </summary>
	[CustomEditor(typeof(AnimationTimeline))]
	public class AnimationTimelineEditor : Editor
	{
		private const float RULER_HEIGHT = 16f;
		private const float LANE_HEIGHT = 18f;
		private const float LANE_PADDING = 2f;
		private const float GRAB_WIDTH = 6f;
		private const int MIN_LANES = 2;
		private const string SNAP_PREF = "SpaxUtils.TimelineEditor.Snap";

		private enum DragMode { None, Start, End, Body }

		private int selected = -1;
		private int dragging = -1;
		private DragMode dragMode;
		private float dragOffset;
		private float playhead;
		private bool scrubbing;
		private bool playing;
		private double lastTick;
		private TimelinePreview preview;
		private GUIStyle headerStyle;
		private readonly List<string> problems = new List<string>();

		private bool Snap
		{
			get => EditorPrefs.GetBool(SNAP_PREF, true);
			set => EditorPrefs.SetBool(SNAP_PREF, value);
		}

		#region Preview

		private void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			preview?.Dispose();
			preview = null;
		}

		private void OnEditorUpdate()
		{
			if (!playing || target == null)
			{
				lastTick = EditorApplication.timeSinceStartup;
				return;
			}

			float extent = ((AnimationTimeline)target).Extent;
			if (extent <= 0f)
			{
				return;
			}

			double now = EditorApplication.timeSinceStartup;
			playhead = Mathf.Repeat(playhead + (float)(now - lastTick), extent);
			lastTick = now;
			Repaint();
		}

		public override bool HasPreviewGUI()
		{
			return ((AnimationTimeline)target).Clip != null;
		}

		public override GUIContent GetPreviewTitle()
		{
			AnimationTimeline timeline = (AnimationTimeline)target;
			float rate = FrameRate(timeline);
			return new GUIContent($"Preview   {playhead:0.000}s   f{Mathf.RoundToInt(playhead * rate)}");
		}

		public override void OnPreviewSettings()
		{
			AnimationTimeline timeline = (AnimationTimeline)target;
			float rate = FrameRate(timeline);

			if (GUILayout.Button(playing ? "❚❚" : "▶", EditorStyles.miniButton, GUILayout.Width(26f)))
			{
				playing = !playing;
				lastTick = EditorApplication.timeSinceStartup;
			}

			if (GUILayout.Button("<", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
			{
				StepFrame(timeline, -1, rate);
			}
			if (GUILayout.Button(">", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
			{
				StepFrame(timeline, 1, rate);
			}

			preview ??= new TimelinePreview();
			preview.DrawSettings();
		}

		private void StepFrame(AnimationTimeline timeline, int direction, float rate)
		{
			playing = false;
			int frame = Mathf.RoundToInt(playhead * rate) + direction;
			playhead = Mathf.Clamp(frame / rate, 0f, timeline.Extent);
			Repaint();
		}

		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
		{
			preview ??= new TimelinePreview();
			preview.Draw(r, ((AnimationTimeline)target).Clip, playhead);
		}

		#endregion Preview

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			AnimationTimeline timeline = (AnimationTimeline)target;

			EditorGUILayout.PropertyField(serializedObject.FindProperty("clip"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("globalData"), true);

			if (timeline.Clip == null)
			{
				EditorGUILayout.HelpBox("Assign an AnimationClip to author markers.", MessageType.Info);
				serializedObject.ApplyModifiedProperties();
				return;
			}

			EditorGUILayout.Space(8);
			DrawTimeline(timeline);
			EditorGUILayout.Space(6);
			DrawToolbar(timeline);
			DrawSelectedMarker(timeline);
			DrawProblems(timeline);

			serializedObject.ApplyModifiedProperties();
		}

		#region Timeline

		private void DrawTimeline(AnimationTimeline timeline)
		{
			IReadOnlyList<TimelineMarker> markers = timeline.Markers;

			// Chicken and egg: lane packing needs pixel widths, the row height needs the lane count.
			// Probe with the inspector width for the height, then repack against the real rect.
			Rect probe = new Rect(0f, 0f, Mathf.Max(64f, EditorGUIUtility.currentViewWidth - 40f), 0f);
			AssignLanes(timeline, probe, out int laneCount);
			float height = RULER_HEIGHT + laneCount * (LANE_HEIGHT + LANE_PADDING) + LANE_PADDING;

			Rect area = EditorGUILayout.GetControlRect(false, height);
			int[] lanes = AssignLanes(timeline, area, out _);

			// Input BEFORE any drawing: the preview reads the playhead too, and processing the drag halfway
			// through the pass leaves everything drawn ahead of it showing the previous value.
			HandleInput(timeline, area, lanes);

			EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));
			DrawRuler(area, timeline, FrameRate(timeline));

			if (markers != null)
			{
				for (int i = 0; i < markers.Count; i++)
				{
					DrawMarker(timeline, area, i, lanes[i]);
				}
			}

			DrawPlayhead(timeline, area);
		}

		private void DrawPlayhead(AnimationTimeline timeline, Rect area)
		{
			float extent = timeline.Extent;
			if (extent <= 0f)
			{
				return;
			}

			playhead = Mathf.Clamp(playhead, 0f, extent);
			float x = TimeToX(area, playhead, extent);
			EditorGUI.DrawRect(new Rect(x - 1f, area.y, 2f, area.height), new Color(1f, 0.9f, 0.3f, 0.9f));
			EditorGUI.DrawRect(new Rect(x - 4f, area.y, 8f, 4f), new Color(1f, 0.9f, 0.3f, 0.9f));
		}

		private void DrawRuler(Rect area, AnimationTimeline timeline, float frameRate)
		{
			float duration = timeline.Extent;
			Rect ruler = new Rect(area.x, area.y, area.width, RULER_HEIGHT);
			EditorGUI.DrawRect(ruler, new Color(0.11f, 0.11f, 0.11f));

			if (duration <= 0f)
			{
				return;
			}

			// Everything past the clip's last frame is sustain, not animation - shade it so a region
			// hanging off the end reads as deliberate rather than as a mistake.
			if (duration > timeline.Duration)
			{
				float clipEndX = TimeToX(area, timeline.Duration, duration);
				EditorGUI.DrawRect(new Rect(clipEndX, area.y, area.xMax - clipEndX, area.height),
					new Color(0f, 0f, 0f, 0.25f));
				EditorGUI.DrawRect(new Rect(clipEndX, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.25f));
			}

			// Per-frame ticks whenever they stay legible - this is the grid markers actually snap to.
			int frames = Mathf.RoundToInt(duration * frameRate);
			if (frames > 0 && area.width / frames >= 3f)
			{
				for (int f = 0; f <= frames; f++)
				{
					float x = TimeToX(area, f / frameRate, duration);
					EditorGUI.DrawRect(new Rect(x, ruler.y + 10f, 1f, 5f), new Color(1f, 1f, 1f, 0.13f));
				}
			}

			// Labelled ticks at a round interval, roughly one per 70px regardless of clip length.
			float step = NiceStep(duration, area.width / 70f);
			for (float time = 0f; time <= duration + 0.0001f; time += step)
			{
				float x = TimeToX(area, time, duration);
				EditorGUI.DrawRect(new Rect(x, ruler.y + 2f, 1f, RULER_HEIGHT - 4f), new Color(1f, 1f, 1f, 0.35f));
				GUI.Label(new Rect(x + 2f, ruler.y - 1f, 70f, RULER_HEIGHT),
					$"{time:0.00}s  f{Mathf.RoundToInt(time * frameRate)}", EditorStyles.miniLabel);
			}
		}

		/// <summary>Rounds a tick interval to something a human reads cleanly at the requested density.</summary>
		private static float NiceStep(float duration, float targetCount)
		{
			float raw = duration / Mathf.Max(1f, targetCount);
			float[] steps = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.25f, 0.5f, 1f, 2f, 5f };
			foreach (float step in steps)
			{
				if (step >= raw)
				{
					return step;
				}
			}
			return 10f;
		}

		private void DrawMarker(AnimationTimeline timeline, Rect area, int index, int lane)
		{
			TimelineMarker marker = timeline.Markers[index];
			if (marker == null || string.IsNullOrEmpty(marker.ID))
			{
				return;
			}

			float duration = timeline.Extent;
			float end = GetEnd(timeline, index);
			bool isRegion = end > marker.Time;
			bool isSelected = index == selected;

			Color color = marker.ID.ToColor();
			float y = area.y + RULER_HEIGHT + LANE_PADDING + lane * (LANE_HEIGHT + LANE_PADDING);
			float startX = TimeToX(area, marker.Time, duration);

			if (isRegion)
			{
				float endX = TimeToX(area, end, duration);
				Rect bar = new Rect(startX, y, Mathf.Max(2f, endX - startX), LANE_HEIGHT);
				EditorGUI.DrawRect(bar, color * new Color(1f, 1f, 1f, isSelected ? 0.55f : 0.3f));
				EditorGUI.DrawRect(new Rect(bar.x, bar.y, 2f, bar.height), color);
				EditorGUI.DrawRect(new Rect(bar.xMax - 2f, bar.y, 2f, bar.height), color);
			}
			else
			{
				EditorGUI.DrawRect(new Rect(startX - 1f, y, 2f, LANE_HEIGHT), color);
			}

			// Full-height playhead line for the selected marker, so it reads against the ruler.
			if (isSelected)
			{
				EditorGUI.DrawRect(new Rect(startX - 1f, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.4f));
			}

			string label = marker.ID.LastDivision();
			GUI.Label(new Rect(startX + 4f, y - 1f, area.width - startX, LANE_HEIGHT), label, EditorStyles.miniLabel);
		}

		private void HandleInput(AnimationTimeline timeline, Rect area, int[] lanes)
		{
			Event e = Event.current;
			float duration = timeline.Extent;
			if (duration <= 0f || timeline.Markers == null)
			{
				return;
			}

			// Capturing hotControl is what keeps a drag alive once the cursor leaves this little rect.
			int control = GUIUtility.GetControlID(FocusType.Passive);

			bool inRuler = e.mousePosition.y <= area.y + RULER_HEIGHT;
			if (e.type == EventType.MouseDown && e.button == 0 && inRuler && area.Contains(e.mousePosition))
			{
				scrubbing = true;
				playing = false;
				GUIUtility.hotControl = control;
				playhead = Mathf.Clamp(SnapTime(timeline, XToTime(area, e.mousePosition.x, duration)), 0f, duration);
				e.Use();
				Repaint();
				return;
			}

			if (e.type == EventType.MouseDrag && scrubbing)
			{
				playhead = Mathf.Clamp(SnapTime(timeline, XToTime(area, e.mousePosition.x, duration)), 0f, duration);
				e.Use();
				Repaint();
				return;
			}

			if (e.type == EventType.MouseUp && scrubbing)
			{
				scrubbing = false;
				GUIUtility.hotControl = 0;
				e.Use();
			}

			if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
			{
				for (int i = 0; i < timeline.Markers.Count; i++)
				{
					TimelineMarker marker = timeline.Markers[i];
					if (marker == null || string.IsNullOrEmpty(marker.ID)) continue;

					float startX = TimeToX(area, marker.Time, duration);
					float endX = TimeToX(area, GetEnd(timeline, i), duration);
					float y = area.y + RULER_HEIGHT + LANE_PADDING + lanes[i] * (LANE_HEIGHT + LANE_PADDING);
					if (e.mousePosition.y < y || e.mousePosition.y > y + LANE_HEIGHT) continue;

					DragMode mode = HitTest(marker, e.mousePosition.x, startX, endX);
					if (mode == DragMode.None) continue;

					selected = i;
					dragging = i;
					dragMode = mode;
					GUIUtility.hotControl = control;
					dragOffset = XToTime(area, e.mousePosition.x, duration) - marker.Time;

					// Park the playhead on what you just grabbed, so the preview shows its pose.
					playhead = marker.Time;
					e.Use();
					Repaint();
					return;
				}
			}

			if (e.type == EventType.MouseDrag && dragging >= 0)
			{
				float grabbed = XToTime(area, e.mousePosition.x, duration);
				SerializedProperty markerProp = serializedObject.FindProperty("markers").GetArrayElementAtIndex(dragging);

				switch (dragMode)
				{
					case DragMode.End:
						float start = markerProp.FindPropertyRelative("time").floatValue;
						markerProp.FindPropertyRelative("duration").floatValue =
							Mathf.Max(0f, SnapTime(timeline, grabbed) - start);
						break;
					case DragMode.Body:
						// Offset by where the bar was grabbed so it doesn't snap its start to the cursor.
						markerProp.FindPropertyRelative("time").floatValue =
							ClampToLinks(timeline, dragging, SnapTime(timeline, grabbed - dragOffset));
						break;
					default:
						markerProp.FindPropertyRelative("time").floatValue =
							ClampToLinks(timeline, dragging, SnapTime(timeline, grabbed));
						break;
				}

				serializedObject.ApplyModifiedProperties();
				timeline.Resolve();

				// Keep the preview on the edge being dragged rather than where it started.
				playhead = markerProp.FindPropertyRelative("time").floatValue;
				if (dragMode == DragMode.End)
				{
					playhead += markerProp.FindPropertyRelative("duration").floatValue;
				}

				e.Use();
				Repaint();
			}

			if (e.type == EventType.MouseUp && dragging >= 0)
			{
				dragging = -1;
				dragMode = DragMode.None;
				GUIUtility.hotControl = 0;
				e.Use();
			}
		}

		/// <summary>
		/// Edges win over the body, except on a region too narrow to hold both handles - there the start wins,
		/// so a thin region can still be moved rather than only ever resized.
		/// </summary>
		private DragMode HitTest(TimelineMarker marker, float mouseX, float startX, float endX)
		{
			bool isRegion = endX > startX;
			bool roomForBothHandles = endX - startX > GRAB_WIDTH * 2f;

			// Only a Duration region resizes; a Marker-ended one follows the marker it points at.
			if (marker.EndMode == MarkerEnd.Duration && roomForBothHandles && Mathf.Abs(mouseX - endX) <= GRAB_WIDTH)
			{
				return DragMode.End;
			}

			if (Mathf.Abs(mouseX - startX) <= GRAB_WIDTH)
			{
				return DragMode.Start;
			}

			return isRegion && mouseX > startX && mouseX < endX ? DragMode.Body : DragMode.None;
		}

		#endregion Timeline

		#region Marker editing

		private void DrawToolbar(AnimationTimeline timeline)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Add Marker"))
				{
					SerializedProperty markersProp = serializedObject.FindProperty("markers");
					markersProp.arraySize++;
					selected = markersProp.arraySize - 1;

					// At the playhead, which is where you were already looking. Growing the array copies the
					// previous element, so the region fields are cleared back to a plain point.
					SerializedProperty added = markersProp.GetArrayElementAtIndex(selected);
					added.FindPropertyRelative("time").floatValue = playhead;
					added.FindPropertyRelative("endMode").enumValueIndex = (int)MarkerEnd.Point;
					added.FindPropertyRelative("duration").floatValue = 0f;
					added.FindPropertyRelative("endMarker").stringValue = string.Empty;
					serializedObject.ApplyModifiedProperties();
					timeline.Resolve();
				}

				int count = timeline.Markers == null ? 0 : timeline.Markers.Count;
				using (new EditorGUI.DisabledScope(selected < 0 || selected >= count))
				{
					if (GUILayout.Button("Remove Selected"))
					{
						serializedObject.FindProperty("markers").DeleteArrayElementAtIndex(selected);
						selected = -1;
						serializedObject.ApplyModifiedProperties();
						timeline.Resolve();
					}
				}

				GUILayout.FlexibleSpace();

				// Snapping is the clip's own frame grid, shown rather than hidden - stepped dragging
				// is otherwise indistinguishable from a broken drag.
				float rate = FrameRate(timeline);
				Snap = GUILayout.Toggle(Snap, Snap ? $"Snap {rate:0} fps" : "Snap off",
					EditorStyles.miniButton, GUILayout.Width(80f));
			}
		}

		private void DrawProblems(AnimationTimeline timeline)
		{
			timeline.Validate(problems);
			foreach (string problem in problems)
			{
				EditorGUILayout.HelpBox(problem, MessageType.Warning);
			}
		}

		private void DrawSelectedMarker(AnimationTimeline timeline)
		{
			SerializedProperty markersProp = serializedObject.FindProperty("markers");
			if (selected < 0 || selected >= markersProp.arraySize)
			{
				EditorGUILayout.HelpBox("Select a marker on the timeline to edit it.", MessageType.None);
				return;
			}

			EditorGUILayout.Space(4);

			// Name in the marker's own colour, so a selected POINT is identifiable at a glance - a region
			// reads from its highlighted bar, but a 2px line does not.
			TimelineMarker marker = timeline.Markers[selected];
			if (marker != null && !string.IsNullOrEmpty(marker.ID))
			{
				EditorGUILayout.LabelField(marker.ID.LastDivision(), HeaderStyle(marker.ID.ToColor()));
			}

			SerializedProperty markerProp = markersProp.GetArrayElementAtIndex(selected);
			SerializedProperty child = markerProp.Copy();
			SerializedProperty endProp = markerProp.GetEndProperty();

			bool enterChildren = true;
			while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, endProp))
			{
				EditorGUILayout.PropertyField(child, true);
				enterChildren = false;
			}

			if (marker == null)
			{
				return;
			}

			float rate = FrameRate(timeline);
			float end = GetEnd(timeline, selected);
			string span = end > marker.Time
				? $"f{Mathf.RoundToInt(marker.Time * rate)} - f{Mathf.RoundToInt(end * rate)}   ({end - marker.Time:0.000}s)"
				: $"f{Mathf.RoundToInt(marker.Time * rate)}";
			EditorGUILayout.LabelField("Frame", $"{span}   of {Mathf.RoundToInt(timeline.Duration * rate)}");
		}

		#endregion Marker editing

		#region Utility

		/// <summary>
		/// Resolves through <see cref="AnimationTimeline.Resolved"/> rather than recomputing, so the drawn
		/// region can never drift from what playback sees. Keyed by index: two markers can share ID and time.
		/// </summary>
		private float GetEnd(AnimationTimeline timeline, int index)
		{
			if (timeline.TryGetResolved(index, out ResolvedMarker resolvedMarker))
			{
				return resolvedMarker.End;
			}

			TimelineMarker marker = timeline.Markers[index];
			return marker == null ? 0f : marker.Time;
		}

		/// <summary>
		/// Packs markers into the fewest non-overlapping lanes. Works in pixels, not seconds, because a point
		/// marker occupies no time yet still needs room for its label.
		/// </summary>
		private int[] AssignLanes(AnimationTimeline timeline, Rect area, out int laneCount)
		{
			IReadOnlyList<TimelineMarker> markers = timeline.Markers;
			int count = markers == null ? 0 : markers.Count;
			int[] lanes = new int[count];
			float duration = timeline.Extent;

			// Pixel span each marker occupies, label included - a point still needs room for its name,
			// which is why a zero-length region cannot share a row with whatever follows it.
			Vector2[] spans = new Vector2[count];
			List<int> order = new List<int>(count);

			for (int i = 0; i < count; i++)
			{
				TimelineMarker marker = markers[i];
				if (marker == null || string.IsNullOrEmpty(marker.ID))
				{
					continue;
				}

				float startX = TimeToX(area, marker.Time, duration);
				spans[i] = new Vector2(startX,
					Mathf.Max(TimeToX(area, GetEnd(timeline, i), duration), startX + LabelWidth(marker.ID)));
				order.Add(i);
			}

			// Widest first, so long regions claim the top rows and short ones settle beneath them rather
			// than whichever happened to start earliest taking the top row.
			order.Sort((a, b) =>
			{
				int widest = (spans[b].y - spans[b].x).CompareTo(spans[a].y - spans[a].x);
				return widest != 0 ? widest : spans[a].x.CompareTo(spans[b].x);
			});

			List<List<Vector2>> occupied = new List<List<Vector2>>();
			foreach (int i in order)
			{
				int lane = 0;
				while (lane < occupied.Count && Overlaps(occupied[lane], spans[i]))
				{
					lane++;
				}

				if (lane == occupied.Count)
				{
					occupied.Add(new List<Vector2>());
				}

				occupied[lane].Add(spans[i]);
				lanes[i] = lane;
			}

			laneCount = Mathf.Max(MIN_LANES, occupied.Count);
			return lanes;
		}

		/// <summary>Touching spans do not overlap, so chained regions stay on a single row.</summary>
		private static bool Overlaps(List<Vector2> lane, Vector2 span)
		{
			foreach (Vector2 other in lane)
			{
				if (span.x < other.y && other.x < span.y)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Keeps chained markers from crossing: a region's start cannot pass the marker that ends it, and a
		/// marker used as someone's end cannot slide in front of that someone's start.
		/// </summary>
		private float ClampToLinks(AnimationTimeline timeline, int index, float time)
		{
			TimelineMarker marker = timeline.Markers[index];
			if (marker == null)
			{
				return time;
			}

			float min = 0f;
			for (int i = 0; i < timeline.Markers.Count; i++)
			{
				TimelineMarker other = timeline.Markers[i];
				if (other == null || i == index)
				{
					continue;
				}

				// This marker terminates 'other', so it can never precede it.
				if (other.EndMode == MarkerEnd.Marker && other.EndMarker == marker.ID)
				{
					min = Mathf.Max(min, other.Time);
				}
			}

			float max = timeline.Extent;
			if (marker.EndMode == MarkerEnd.Marker)
			{
				for (int i = 0; i < timeline.Markers.Count; i++)
				{
					TimelineMarker other = timeline.Markers[i];

					// Only candidates at or after our floor can be the one we actually bind to.
					if (other == null || i == index || other.ID != marker.EndMarker || other.Time < min)
					{
						continue;
					}

					max = Mathf.Min(max, other.Time);
				}
			}

			return Mathf.Clamp(time, min, Mathf.Max(min, max));
		}

		/// <summary>Pixel room a marker's label claims, so co-located points never draw over each other.</summary>
		private static float LabelWidth(string id)
		{
			return EditorStyles.miniLabel.CalcSize(new GUIContent(id.LastDivision())).x + 10f;
		}

		private float TimeToX(Rect area, float time, float duration)
		{
			return duration <= 0f ? area.x : area.x + Mathf.Clamp01(time / duration) * area.width;
		}

		private float XToTime(Rect area, float x, float duration)
		{
			return area.width <= 0f ? 0f : Mathf.Clamp01((x - area.x) / area.width) * duration;
		}

		/// <summary>Snaps to the clip's own frame grid; hold Alt to place freely for one drag.</summary>
		private float SnapTime(AnimationTimeline timeline, float time)
		{
			if (!Snap || (Event.current != null && Event.current.alt))
			{
				return time;
			}

			float rate = FrameRate(timeline);
			return Mathf.Round(time * rate) / rate;
		}

		private static float FrameRate(AnimationTimeline timeline)
		{
			return timeline.Clip != null && timeline.Clip.frameRate > 0f ? timeline.Clip.frameRate : 60f;
		}

		/// <summary>
		/// Own style instance, recoloured per call. Tinting EditorStyles directly leaks into every other
		/// label Unity draws with it.
		/// </summary>
		private GUIStyle HeaderStyle(Color color)
		{
			headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
			headerStyle.normal.textColor = color;
			return headerStyle;
		}

		#endregion Utility
	}
}
