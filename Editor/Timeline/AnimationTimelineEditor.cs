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
		private const float MIN_PREVIEW_HEIGHT = 80f;
		private const int CLOCK_FONT_SIZE = 14;
		private const float CURVE_HEADER_HEIGHT = 18f;
		private const float CURVE_GRAPH_HEIGHT = 72f;
		private const float MIN_VIEW_SPAN = 0.02f;
		private const float ZOOM_RATE = 1.12f;
		private const string SNAP_PREF = "SpaxUtils.TimelineEditor.Snap";
		private const string CURVE_PREF = "SpaxUtils.TimelineEditor.Curve";
		private const string CURVE_NORMALIZED_PREF = "SpaxUtils.TimelineEditor.CurveNormalized";

		private enum DragMode { None, Start, End, Body }

		/// <summary>
		/// Identifiers a host offers for this timeline's context, driving the Add Marker dropdown.
		/// Left null the button simply adds a blank marker.
		/// </summary>
		public IReadOnlyList<string> Suggestions { get; set; }

		private bool Snap
		{
			get => EditorPrefs.GetBool(SNAP_PREF, true);
			set => EditorPrefs.SetBool(SNAP_PREF, value);
		}

		/// <summary>Identifier of the global curve on show. A view setting, so it lives in prefs, not the asset.</summary>
		private string SelectedCurve
		{
			get => EditorPrefs.GetString(CURVE_PREF, string.Empty);
			set => EditorPrefs.SetString(CURVE_PREF, value);
		}

		/// <summary>
		/// Whether the curve's own 0..1 domain is stretched across the timeline. CHARGE_WEIGHT is authored over
		/// charge progress, not clip seconds, so the two kinds need different readings of the same axis.
		/// </summary>
		private bool NormalizedCurve
		{
			get => EditorPrefs.GetBool(CURVE_NORMALIZED_PREF, false);
			set => EditorPrefs.SetBool(CURVE_NORMALIZED_PREF, value);
		}

		private int selected = -1;
		private int dragging = -1;
		private DragMode dragMode;
		private float dragOffset;
		private float playhead;
		private float viewStart;
		private float viewEnd;
		private bool scrubbing;
		private bool playing;
		private double lastTick;
		private TimelinePreview preview;
		private GUIStyle headerStyle;
		private GUIStyle clockStyle;
		private readonly List<string> problems = new List<string>();

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
			// Just the name: the clock is stamped onto the preview itself, where it is actually readable.
			return new GUIContent("Preview");
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

			// Zoom has no other way back once you are deep in a clip.
			if (GUILayout.Button(new GUIContent("Frame", "Zoom out to the whole timeline."),
				EditorStyles.miniButton, GUILayout.Width(44f)))
			{
				viewStart = 0f;
				viewEnd = timeline.Extent;
				Repaint();
			}

			// Snapping rides with the transport, now that the timeline it governs lives in this same pane.
			Snap = GUILayout.Toggle(Snap,
				new GUIContent("Snap", $"Snap to the clip's {rate:0} fps grid. Hold Alt to place freely."),
				EditorStyles.miniButton, GUILayout.Width(42f));

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

		/// <summary>
		/// The timeline is glued to the top of the preview: you scrub markers here and the pose they land on
		/// is directly below, rather than a panel away in the inspector body.
		/// </summary>
		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
		{
			AnimationTimeline timeline = (AnimationTimeline)target;
			float extent = timeline.Extent;
			ClampView(extent);

			// Never let the strip and curve lane swallow the rig; they yield, the preview keeps its floor.
			float budget = Mathf.Max(0f, r.height - MIN_PREVIEW_HEIGHT);

			// View input first: every height below is measured through the mapping it changes.
			HandleView(new Rect(r.x, r.y, r.width, budget), extent);

			float strip = Mathf.Min(TimelineHeight(timeline, r.width), budget);
			float y = r.y;
			if (strip >= RULER_HEIGHT)
			{
				DrawTimeline(timeline, new Rect(r.x, y, r.width, strip));
				y += strip;
			}

			float lane = Mathf.Min(CurveLaneHeight(), Mathf.Max(0f, r.y + budget - y));
			if (lane >= CURVE_HEADER_HEIGHT)
			{
				DrawCurveLane(timeline, new Rect(r.x, y, r.width, lane));
				y += lane;
			}

			Rect area = new Rect(r.x, y, r.width, r.yMax - y);
			preview ??= new TimelinePreview();
			preview.Draw(area, timeline.Clip, playhead);
			DrawClock(area, FrameRate(timeline));
		}

		/// <summary>Stamped over the preview rather than tucked into the header bar, where it reads as chrome.</summary>
		private void DrawClock(Rect area, float rate)
		{
			GUIStyle style = ClockStyle();
			GUIContent content = new GUIContent($"{playhead:0.000}s   f{Mathf.RoundToInt(playhead * rate)}");
			Vector2 size = style.CalcSize(content);

			Rect backing = new Rect(area.x + 6f, area.y + 6f, size.x + 10f, size.y + 4f);
			EditorGUI.DrawRect(backing, new Color(0f, 0f, 0f, 0.45f));
			GUI.Label(new Rect(backing.x + 5f, backing.y + 2f, size.x, size.y), content, style);
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

			EditorGUILayout.Space(6);
			DrawToolbar(timeline);
			DrawSelectedMarker(timeline);
			DrawProblems(timeline);

			serializedObject.ApplyModifiedProperties();
		}

		#region Timeline

		/// <summary>
		/// Height the timeline needs at this width. Lane packing works in pixels, so the lane count - and
		/// with it the height - cannot be known until the width is.
		/// </summary>
		private float TimelineHeight(AnimationTimeline timeline, float width)
		{
			AssignLanes(timeline, new Rect(0f, 0f, Mathf.Max(64f, width), 0f), out int laneCount);
			return RULER_HEIGHT + laneCount * (LANE_HEIGHT + LANE_PADDING) + LANE_PADDING;
		}

		private void DrawTimeline(AnimationTimeline timeline, Rect area)
		{
			IReadOnlyList<TimelineMarker> markers = timeline.Markers;
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
			float x = TimeToX(area, playhead);
			if (x < area.x || x > area.xMax)
			{
				return;
			}

			EditorGUI.DrawRect(new Rect(x - 1f, area.y, 2f, area.height), new Color(1f, 0.9f, 0.3f, 0.9f));
			EditorGUI.DrawRect(new Rect(x - 4f, area.y, 8f, 4f), new Color(1f, 0.9f, 0.3f, 0.9f));
		}

		private void DrawRuler(Rect area, AnimationTimeline timeline, float frameRate)
		{
			Rect ruler = new Rect(area.x, area.y, area.width, RULER_HEIGHT);
			EditorGUI.DrawRect(ruler, new Color(0.11f, 0.11f, 0.11f));

			float span = viewEnd - viewStart;
			if (span <= 0f)
			{
				return;
			}

			// Everything past the clip's last frame is sustain, not animation - shade it so a region
			// hanging off the end reads as deliberate rather than as a mistake.
			if (timeline.Extent > timeline.Duration && viewEnd > timeline.Duration)
			{
				float clipEndX = Mathf.Max(area.x, TimeToX(area, timeline.Duration));
				EditorGUI.DrawRect(new Rect(clipEndX, area.y, area.xMax - clipEndX, area.height),
					new Color(0f, 0f, 0f, 0.25f));
				EditorGUI.DrawRect(new Rect(clipEndX, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.25f));
			}

			// Ticks are walked across the VISIBLE window only, so zooming in never costs a pass over a long clip.
			// Per-frame ticks appear once they are legible - this is the grid markers actually snap to.
			if (area.width / (span * frameRate) >= 3f)
			{
				int first = Mathf.FloorToInt(viewStart * frameRate);
				int last = Mathf.CeilToInt(viewEnd * frameRate);
				for (int f = first; f <= last; f++)
				{
					float x = TimeToX(area, f / frameRate);
					EditorGUI.DrawRect(new Rect(x, ruler.y + 10f, 1f, 5f), new Color(1f, 1f, 1f, 0.13f));
				}
			}

			// Labelled ticks at a round interval, roughly one per 70px whatever the zoom.
			float step = NiceStep(span, area.width / 70f);
			float start = Mathf.Floor(viewStart / step) * step;
			for (float time = start; time <= viewEnd + 0.0001f; time += step)
			{
				float x = TimeToX(area, time);
				if (x < area.x - 1f)
				{
					continue;
				}

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

			float end = GetEnd(timeline, index);
			bool isRegion = end > marker.Time;
			bool isSelected = index == selected;

			// Scrolled out of view entirely; a region straddling the window still draws, clipped to its edges.
			if (end < viewStart || marker.Time > viewEnd)
			{
				return;
			}

			Color color = marker.ID.ToColor();
			float y = area.y + RULER_HEIGHT + LANE_PADDING + lane * (LANE_HEIGHT + LANE_PADDING);
			float startX = TimeToX(area, marker.Time);

			if (isRegion)
			{
				float left = Mathf.Max(area.x, startX);
				float right = Mathf.Min(area.xMax, TimeToX(area, end));
				Rect bar = new Rect(left, y, Mathf.Max(2f, right - left), LANE_HEIGHT);
				EditorGUI.DrawRect(bar, color * new Color(1f, 1f, 1f, isSelected ? 0.55f : 0.3f));

				// Only draw an edge that is actually in view, or a clipped region grows false handles.
				if (startX >= area.x)
				{
					EditorGUI.DrawRect(new Rect(bar.x, bar.y, 2f, bar.height), color);
				}
				if (right <= area.xMax - 2f)
				{
					EditorGUI.DrawRect(new Rect(bar.xMax - 2f, bar.y, 2f, bar.height), color);
				}
			}
			else
			{
				EditorGUI.DrawRect(new Rect(startX - 1f, y, 2f, LANE_HEIGHT), color);
			}

			// Full-height playhead line for the selected marker, so it reads against the ruler.
			if (isSelected && startX >= area.x && startX <= area.xMax)
			{
				EditorGUI.DrawRect(new Rect(startX - 1f, area.y, 1f, area.height), new Color(1f, 1f, 1f, 0.4f));
			}

			// Labels ride along the left edge while their region is still partly on screen.
			float labelX = Mathf.Max(area.x + 2f, startX + 4f);
			string label = marker.ID.LastDivision();
			GUI.Label(new Rect(labelX, y - 1f, area.xMax - labelX, LANE_HEIGHT), label, EditorStyles.miniLabel);
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
				playhead = Mathf.Clamp(SnapTime(timeline, XToTime(area, e.mousePosition.x)), 0f, duration);
				e.Use();
				Repaint();
				return;
			}

			if (e.type == EventType.MouseDrag && scrubbing)
			{
				playhead = Mathf.Clamp(SnapTime(timeline, XToTime(area, e.mousePosition.x)), 0f, duration);
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

					float startX = TimeToX(area, marker.Time);
					float endX = TimeToX(area, GetEnd(timeline, i));
					float y = area.y + RULER_HEIGHT + LANE_PADDING + lanes[i] * (LANE_HEIGHT + LANE_PADDING);
					if (e.mousePosition.y < y || e.mousePosition.y > y + LANE_HEIGHT) continue;

					DragMode mode = HitTest(marker, e.mousePosition.x, startX, endX);
					if (mode == DragMode.None) continue;

					selected = i;
					dragging = i;
					dragMode = mode;
					GUIUtility.hotControl = control;
					dragOffset = XToTime(area, e.mousePosition.x) - marker.Time;

					// Park the playhead on what you just grabbed, so the preview shows its pose.
					playhead = marker.Time;
					e.Use();
					Repaint();
					return;
				}
			}

			if (e.type == EventType.MouseDrag && dragging >= 0)
			{
				float grabbed = XToTime(area, e.mousePosition.x);
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
		/// Zoom and pan across the whole band, so the gesture works over the curve lane too and both stay in
		/// step. Scroll zooms around the cursor; middle-drag pans, which no other control here claims.
		/// </summary>
		private void HandleView(Rect band, float extent)
		{
			Event e = Event.current;
			if (!band.Contains(e.mousePosition))
			{
				return;
			}

			if (e.type == EventType.ScrollWheel)
			{
				// Anchor on the time under the cursor so it stays put while everything else spreads around it.
				float pivot = XToTime(band, e.mousePosition.x);
				float fraction = Mathf.InverseLerp(viewStart, viewEnd, pivot);
				float span = (viewEnd - viewStart) * Mathf.Pow(ZOOM_RATE, e.delta.y);

				viewStart = pivot - fraction * span;
				viewEnd = viewStart + span;
				ClampView(extent);
				e.Use();
				Repaint();
				return;
			}

			if (e.type == EventType.MouseDrag && e.button == 2)
			{
				float shift = -e.delta.x * (viewEnd - viewStart) / Mathf.Max(1f, band.width);
				viewStart += shift;
				viewEnd += shift;
				ClampView(extent);
				e.Use();
				Repaint();
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

		#region Curve lane

		/// <summary>Header only until a curve is picked, so an unused lane costs one row rather than a panel.</summary>
		private float CurveLaneHeight()
		{
			return string.IsNullOrEmpty(SelectedCurve) ? CURVE_HEADER_HEIGHT : CURVE_HEADER_HEIGHT + CURVE_GRAPH_HEIGHT;
		}

		/// <summary>
		/// A single global curve under the timeline, read straight from the asset each repaint so edits made
		/// anywhere else show up live. Read-only by design: this is for seeing WHERE a value turns, not authoring it.
		/// </summary>
		private void DrawCurveLane(AnimationTimeline timeline, Rect area)
		{
			Rect header = new Rect(area.x, area.y, area.width, CURVE_HEADER_HEIGHT);
			EditorGUI.DrawRect(header, new Color(0.13f, 0.13f, 0.13f));

			AnimationCurve curve = FindCurve(timeline, SelectedCurve);
			Rect dropdown = new Rect(header.x + 2f, header.y + 1f, 170f, CURVE_HEADER_HEIGHT - 3f);
			string label = string.IsNullOrEmpty(SelectedCurve) ? "Curve: none" : SelectedCurve.LastDivision();
			if (EditorGUI.DropdownButton(dropdown, new GUIContent(label, SelectedCurve), FocusType.Passive, EditorStyles.miniPullDown))
			{
				ShowCurveMenu(timeline, dropdown);
			}

			if (curve == null)
			{
				return;
			}

			Rect toggle = new Rect(dropdown.xMax + 4f, header.y, 84f, CURVE_HEADER_HEIGHT - 2f);
			NormalizedCurve = GUI.Toggle(toggle, NormalizedCurve,
				new GUIContent("Normalized", "Stretch the curve's own 0..1 domain across the timeline, for curves authored over progress rather than seconds."),
				EditorStyles.miniButton);

			Rect graph = new Rect(area.x, header.yMax, area.width, area.height - CURVE_HEADER_HEIGHT);
			HandleCurveScrub(timeline, graph);
			DrawCurveGraph(graph, curve, timeline.Extent, out float low, out float high);

			// Value under the playhead, right where you are looking for the moment it turns.
			GUI.Label(new Rect(toggle.xMax + 6f, header.y, area.width, CURVE_HEADER_HEIGHT),
				$"{Sample(curve, playhead, timeline.Extent):0.000}      [{low:0.00} .. {high:0.00}]",
				EditorStyles.miniLabel);
		}

		/// <summary>
		/// Starts a scrub from the curve itself - reading where a value turns is exactly when you want to park
		/// the playhead there. The strip's own handler carries the drag from here, since both share the x axis.
		/// </summary>
		private void HandleCurveScrub(AnimationTimeline timeline, Rect area)
		{
			Event e = Event.current;
			int control = GUIUtility.GetControlID(FocusType.Passive);

			if (e.type != EventType.MouseDown || e.button != 0 || !area.Contains(e.mousePosition))
			{
				return;
			}

			scrubbing = true;
			playing = false;
			GUIUtility.hotControl = control;
			playhead = Mathf.Clamp(SnapTime(timeline, XToTime(area, e.mousePosition.x)), 0f, timeline.Extent);
			e.Use();
			Repaint();
		}

		private void DrawCurveGraph(Rect area, AnimationCurve curve, float extent, out float low, out float high)
		{
			EditorGUI.DrawRect(area, new Color(0.1f, 0.1f, 0.1f));
			CurveRange(curve, extent, out low, out high);

			// Guides at 0 and 1: weights live between them, so they are the reference you actually read against.
			DrawGuide(area, 0f, low, high, new Color(1f, 1f, 1f, 0.18f));
			DrawGuide(area, 1f, low, high, new Color(1f, 1f, 1f, 0.10f));

			if (Event.current.type == EventType.Repaint && curve.length > 0)
			{
				int steps = Mathf.Clamp(Mathf.RoundToInt(area.width), 2, 512);
				Vector3[] points = new Vector3[steps + 1];
				for (int i = 0; i <= steps; i++)
				{
					float x = area.x + area.width * i / steps;
					float value = Sample(curve, XToTime(area, x), extent);
					points[i] = new Vector3(x, ValueToY(area, value, low, high), 0f);
				}

				Handles.color = new Color(0.45f, 0.85f, 1f, 0.95f);
				Handles.DrawAAPolyLine(2f, points);
			}

			// Keys as ticks: the shape says how it moves, these say where it was actually authored to.
			foreach (Keyframe key in curve.keys)
			{
				float time = NormalizedCurve ? key.time * Mathf.Max(MIN_VIEW_SPAN, extent) : key.time;
				float x = TimeToX(area, time);
				if (x < area.x || x > area.xMax)
				{
					continue;
				}

				float y = ValueToY(area, key.value, low, high);
				EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, 4f, 4f), new Color(1f, 0.9f, 0.3f, 0.95f));
			}

			float playheadX = TimeToX(area, playhead);
			if (playheadX >= area.x && playheadX <= area.xMax)
			{
				EditorGUI.DrawRect(new Rect(playheadX - 1f, area.y, 2f, area.height), new Color(1f, 0.9f, 0.3f, 0.5f));
			}
		}

		/// <summary>
		/// Range taken from the WHOLE curve rather than the visible slice, so panning never rescales the shape
		/// under you. Sampled, not keyed - tangent overshoot leaves the key values behind.
		/// </summary>
		private void CurveRange(AnimationCurve curve, float extent, out float low, out float high)
		{
			low = float.MaxValue;
			high = float.MinValue;

			for (int i = 0; i <= 128; i++)
			{
				float value = Sample(curve, extent * i / 128f, extent);
				low = Mathf.Min(low, value);
				high = Mathf.Max(high, value);
			}

			if (low > high)
			{
				low = 0f;
				high = 1f;
			}

			float pad = Mathf.Max(0.05f, (high - low) * 0.1f);
			low -= pad;
			high += pad;
		}

		private float Sample(AnimationCurve curve, float time, float extent)
		{
			return curve.Evaluate(NormalizedCurve ? time / Mathf.Max(MIN_VIEW_SPAN, extent) : time);
		}

		private static float ValueToY(Rect area, float value, float low, float high)
		{
			float t = high > low ? (value - low) / (high - low) : 0.5f;
			return Mathf.Clamp(area.yMax - t * area.height, area.yMin, area.yMax);
		}

		private static void DrawGuide(Rect area, float value, float low, float high, Color color)
		{
			if (value < low || value > high)
			{
				return;
			}

			EditorGUI.DrawRect(new Rect(area.x, ValueToY(area, value, low, high), area.width, 1f), color);
		}

		private void ShowCurveMenu(AnimationTimeline timeline, Rect rect)
		{
			GenericMenu menu = new GenericMenu();
			menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(SelectedCurve), () => SelectCurve(string.Empty));

			bool any = false;
			foreach (ILabeledData data in CurveData(timeline))
			{
				any = true;
				string id = data.ID;
				menu.AddItem(new GUIContent(id.LastDivision()), id == SelectedCurve, () => SelectCurve(id));
			}

			if (!any)
			{
				menu.AddDisabledItem(new GUIContent("No curves in this timeline's Global Data"));
			}

			menu.DropDown(rect);
		}

		private void SelectCurve(string id)
		{
			SelectedCurve = id;
			Repaint();
		}

		private static AnimationCurve FindCurve(AnimationTimeline timeline, string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			foreach (ILabeledData data in CurveData(timeline))
			{
				if (data.ID == id)
				{
					return (AnimationCurve)data.Value;
				}
			}

			return null;
		}

		private static IEnumerable<ILabeledData> CurveData(AnimationTimeline timeline)
		{
			if (timeline.GlobalData == null)
			{
				yield break;
			}

			foreach (ILabeledData data in timeline.GlobalData.LabeledData)
			{
				if (data.ValueType == typeof(AnimationCurve) && data.Value != null)
				{
					yield return data;
				}
			}
		}

		#endregion Curve lane

		#region Marker editing

		private void DrawToolbar(AnimationTimeline timeline)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				bool suggest = Suggestions != null && Suggestions.Count > 0;
				if (GUILayout.Button(suggest ? "Add Marker  ▾" : "Add Marker"))
				{
					if (suggest)
					{
						ShowAddMenu(timeline, GUILayoutUtility.GetLastRect());
					}
					else
					{
						AddMarker(timeline, string.Empty);
					}
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
			}
		}

		/// <summary>
		/// Offers the identifiers the host says are meaningful here, ticking the ones already present.
		/// Duplicates stay selectable - two hit windows on one swing is a legitimate thing to author.
		/// </summary>
		private void ShowAddMenu(AnimationTimeline timeline, Rect rect)
		{
			GenericMenu menu = new GenericMenu();
			foreach (string id in Suggestions)
			{
				string suggestion = id;
				menu.AddItem(new GUIContent(id.LastDivision()), timeline.TryGetMarker(id, out _),
					() => AddMarker(timeline, suggestion));
			}

			menu.AddSeparator(string.Empty);
			menu.AddItem(new GUIContent("Blank"), false, () => AddMarker(timeline, string.Empty));
			menu.DropDown(rect);
		}

		/// <summary>Adds a marker at the playhead, which is where you were already looking.</summary>
		private void AddMarker(AnimationTimeline timeline, string id)
		{
			// Menu callbacks land outside the GUI pass, so re-sync before writing rather than applying stale state.
			serializedObject.Update();
			TimelineAuthoring.AddMarker(serializedObject, id, playhead);
			selected = serializedObject.FindProperty("markers").arraySize - 1;
			serializedObject.ApplyModifiedProperties();
			timeline.Resolve();
			Repaint();
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

				float startX = TimeToX(area, marker.Time);
				spans[i] = new Vector2(startX,
					Mathf.Max(TimeToX(area, GetEnd(timeline, i)), startX + LabelWidth(marker.ID)));
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

		/// <summary>
		/// Maps against the VISIBLE window rather than the whole timeline, which is the whole of zoom and pan -
		/// every ruler tick, marker, lane and hit-test goes through here, so they all follow for free.
		/// Deliberately unclamped: callers clip their own rects, and clamping would pile offscreen markers at the edge.
		/// </summary>
		private float TimeToX(Rect area, float time)
		{
			float span = viewEnd - viewStart;
			return span <= 0f ? area.x : area.x + (time - viewStart) / span * area.width;
		}

		private float XToTime(Rect area, float x)
		{
			float span = viewEnd - viewStart;
			return area.width <= 0f ? viewStart : viewStart + (x - area.x) / area.width * span;
		}

		/// <summary>Keeps the window inside the content and never narrower than a couple of frames.</summary>
		private void ClampView(float extent)
		{
			extent = Mathf.Max(MIN_VIEW_SPAN, extent);

			// An unset window (a freshly opened inspector) frames everything.
			if (viewEnd <= viewStart)
			{
				viewStart = 0f;
				viewEnd = extent;
				return;
			}

			float span = Mathf.Clamp(viewEnd - viewStart, MIN_VIEW_SPAN, extent);
			viewStart = Mathf.Clamp(viewStart, 0f, extent - span);
			viewEnd = viewStart + span;
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
		/// <summary>Own instance for the same reason as <see cref="HeaderStyle"/>: EditorStyles are shared.</summary>
		private GUIStyle ClockStyle()
		{
			clockStyle ??= new GUIStyle(EditorStyles.miniLabel) { fontSize = CLOCK_FONT_SIZE };
			clockStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
			return clockStyle;
		}

		private GUIStyle HeaderStyle(Color color)
		{
			headerStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
			headerStyle.normal.textColor = color;
			return headerStyle;
		}

		#endregion Utility
	}
}
