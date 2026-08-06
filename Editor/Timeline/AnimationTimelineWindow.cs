using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Large scrub view for an <see cref="AnimationTimeline"/>. The asset inspector carries the same preview
	/// inline; this exists for when the inspector pane is too small to judge a pose in.
	/// </summary>
	public class AnimationTimelineWindow : EditorWindow
	{
		private const float STRIP_HEIGHT = 14f;

		private AnimationTimeline timeline;
		private float time;
		private bool playing;
		private double lastTick;
		private TimelinePreview preview;

		[MenuItem("Tools/Animation Timeline")]
		private static void Open()
		{
			GetWindow<AnimationTimelineWindow>("Timeline").Show();
		}

		private void OnEnable()
		{
			preview ??= new TimelinePreview();
			if (timeline == null)
			{
				timeline = Selection.activeObject as AnimationTimeline;
			}
			EditorApplication.update += OnEditorUpdate;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			preview?.Dispose();
			preview = null;
		}

		private void OnSelectionChange()
		{
			if (Selection.activeObject is AnimationTimeline selected)
			{
				timeline = selected;
				time = 0f;
				Repaint();
			}
		}

		private void OnEditorUpdate()
		{
			if (!playing || timeline == null || timeline.Extent <= 0f)
			{
				lastTick = EditorApplication.timeSinceStartup;
				return;
			}

			double now = EditorApplication.timeSinceStartup;
			time = Mathf.Repeat(time + (float)(now - lastTick), timeline.Extent);
			lastTick = now;
			Repaint();
		}

		private void OnGUI()
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
			{
				EditorGUI.BeginChangeCheck();
				timeline = (AnimationTimeline)EditorGUILayout.ObjectField(timeline, typeof(AnimationTimeline), false);
				if (EditorGUI.EndChangeCheck())
				{
					time = 0f;
				}

				preview ??= new TimelinePreview();
				preview.DrawSettings();
			}

			if (timeline == null || timeline.Clip == null)
			{
				EditorGUILayout.HelpBox("Select an AnimationTimeline with a clip assigned.", MessageType.Info);
				return;
			}

			Rect area = GUILayoutUtility.GetRect(position.width, Mathf.Max(160f, position.height - 150f));
			preview.Draw(area, timeline.Clip, time);

			DrawTransport();
			DrawStrip();
			DrawActiveMarkers();
		}

		#region Sections

		private void DrawTransport()
		{
			float rate = timeline.Clip != null && timeline.Clip.frameRate > 0f ? timeline.Clip.frameRate : 60f;

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(56f)))
				{
					playing = !playing;
					lastTick = EditorApplication.timeSinceStartup;
				}

				// Frame stepping, since markers sit on frames and dragging cannot land them precisely.
				if (GUILayout.Button("<", EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
				{
					StepFrame(-1, rate);
				}
				if (GUILayout.Button(">", EditorStyles.miniButtonRight, GUILayout.Width(24f)))
				{
					StepFrame(1, rate);
				}

				time = EditorGUILayout.Slider(time, 0f, timeline.Extent);

				GUILayout.Label($"{time:0.000}s   f{Mathf.RoundToInt(time * rate)}/{Mathf.RoundToInt(timeline.Extent * rate)}",
					EditorStyles.miniLabel, GUILayout.Width(150f));
			}
		}

		private void StepFrame(int direction, float rate)
		{
			playing = false;
			int frame = Mathf.RoundToInt(time * rate) + direction;
			time = Mathf.Clamp(frame / rate, 0f, timeline.Extent);
		}

		private void DrawStrip()
		{
			Rect area = EditorGUILayout.GetControlRect(false, STRIP_HEIGHT * 2f);
			EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));

			float extent = timeline.Extent;
			if (extent <= 0f)
			{
				return;
			}

			// Sustain past the clip's final frame, shaded so it reads as deliberate.
			if (extent > timeline.Duration)
			{
				float clipEndX = area.x + Mathf.Clamp01(timeline.Duration / extent) * area.width;
				EditorGUI.DrawRect(new Rect(clipEndX, area.y, area.xMax - clipEndX, area.height), new Color(0f, 0f, 0f, 0.25f));
			}

			foreach (ResolvedMarker marker in timeline.Resolved)
			{
				Color color = marker.ID.ToColor();
				float startX = area.x + Mathf.Clamp01(marker.Start / extent) * area.width;

				if (marker.IsRegion)
				{
					float endX = area.x + Mathf.Clamp01(marker.End / extent) * area.width;
					EditorGUI.DrawRect(new Rect(startX, area.y, Mathf.Max(2f, endX - startX), STRIP_HEIGHT),
						color * new Color(1f, 1f, 1f, 0.35f));
				}
				else
				{
					EditorGUI.DrawRect(new Rect(startX - 1f, area.y, 2f, STRIP_HEIGHT), color);
				}
			}

			float playheadX = area.x + Mathf.Clamp01(time / extent) * area.width;
			EditorGUI.DrawRect(new Rect(playheadX - 1f, area.y, 2f, area.height), Color.white);

			HandleScrub(area, extent);
		}

		private void DrawActiveMarkers()
		{
			bool any = false;
			foreach (ResolvedMarker marker in timeline.Resolved)
			{
				if (!marker.Contains(time))
				{
					continue;
				}

				any = true;
				string detail = marker.IsRegion
					? $"{marker.Start:0.000} .. {marker.End:0.000}  ({marker.Progress(time):P0})"
					: $"{marker.Start:0.000}";
				EditorGUILayout.LabelField(marker.ID.LastDivision(), detail, EditorStyles.miniLabel);
			}

			if (!any)
			{
				EditorGUILayout.LabelField("Active", "-", EditorStyles.miniLabel);
			}
		}

		private void HandleScrub(Rect area, float extent)
		{
			Event e = Event.current;
			if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 &&
				area.Contains(e.mousePosition))
			{
				time = Mathf.Clamp01((e.mousePosition.x - area.x) / area.width) * extent;
				playing = false;
				e.Use();
				Repaint();
			}
		}

		#endregion Sections
	}
}
