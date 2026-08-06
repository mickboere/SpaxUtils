using System;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Renders a rig posed at a point in an <see cref="AnimationClip"/>. Shared by the timeline inspector
	/// and window so there is only one preview implementation to keep correct.
	/// </summary>
	public class TimelinePreview : IDisposable
	{
		private const string PREFAB_PREF = "SpaxUtils.TimelinePreview.Rig";

		/// <summary>The rig to pose. Per-user, not a project setting - creatures use their own prefabs.</summary>
		public GameObject Prefab
		{
			get => prefab;
			set
			{
				if (prefab == value)
				{
					return;
				}

				prefab = value;

				// Scene objects have no asset path; remember only real assets rather than clearing the
				// stored prefab because something unsaveable was dropped in.
				string path = value == null ? string.Empty : AssetDatabase.GetAssetPath(value);
				if (value == null || !string.IsNullOrEmpty(path))
				{
					EditorPrefs.SetString(PREFAB_PREF, path);
				}

				DestroyInstance();
			}
		}

		private GameObject prefab;
		private PreviewRenderUtility preview;
		private GameObject instance;
		private Mesh grid;
		private Material gridMaterial;
		private Vector2 orbit = new Vector2(140f, 10f);
		private float distance = 3.5f;
		private float pivotHeight = 1f;

		public TimelinePreview()
		{
			string path = EditorPrefs.GetString(PREFAB_PREF, string.Empty);
			if (!string.IsNullOrEmpty(path))
			{
				prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			}
		}

		/// <summary>Draws the rig posed at <paramref name="time"/>, handling orbit, zoom and prefab drops.</summary>
		public void Draw(Rect rect, AnimationClip clip, float time)
		{
			HandleDragAndDrop(rect);

			if (Prefab == null)
			{
				EditorGUI.LabelField(rect, "Drag a rig prefab here to preview.", EditorStyles.centeredGreyMiniLabel);
				return;
			}

			if (clip == null)
			{
				EditorGUI.LabelField(rect, "Timeline has no clip.", EditorStyles.centeredGreyMiniLabel);
				return;
			}

			HandleNavigation(rect);

			// URP sizes its render targets from this rect and throws inside the render graph rather than
			// no-oping, so a degenerate rect or a non-Repaint pass must never reach BeginPreview.
			if (Event.current.type != EventType.Repaint || rect.width < 1f || rect.height < 1f)
			{
				return;
			}

			Ensure();
			if (preview == null || instance == null)
			{
				return;
			}

			preview.BeginPreview(rect, GUIStyle.none);

			// SampleAnimation poses the instance directly - no Animator state, no graph, no play mode.
			// Times past the clip clamp to its final frame, which is what a sustaining region should show.
			clip.SampleAnimation(instance, Mathf.Clamp(time, 0f, clip.length));
			PositionCamera();
			DrawGrid();
			preview.camera.Render();

			GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
		}

		/// <summary>Prefab picker, sized for an inspector preview header.</summary>
		public void DrawSettings()
		{
			Prefab = (GameObject)EditorGUILayout.ObjectField(Prefab, typeof(GameObject), false, GUILayout.Width(140f));
		}

		public void Dispose()
		{
			DestroyInstance();

			if (grid != null)
			{
				UnityEngine.Object.DestroyImmediate(grid);
				grid = null;
			}

			if (gridMaterial != null)
			{
				UnityEngine.Object.DestroyImmediate(gridMaterial);
				gridMaterial = null;
			}

			if (preview != null)
			{
				preview.Cleanup();
				preview = null;
			}
		}

		#region Internals

		/// <summary>Accepts a GameObject dropped anywhere on the preview, not just onto the settings field.</summary>
		private void HandleDragAndDrop(Rect rect)
		{
			Event e = Event.current;
			if ((e.type != EventType.DragUpdated && e.type != EventType.DragPerform) || !rect.Contains(e.mousePosition))
			{
				return;
			}

			GameObject dropped = null;
			foreach (UnityEngine.Object reference in DragAndDrop.objectReferences)
			{
				if (reference is GameObject gameObject)
				{
					dropped = gameObject;
					break;
				}
			}

			if (dropped == null)
			{
				return;
			}

			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			if (e.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				Prefab = dropped;
			}
			e.Use();
		}

		private void HandleNavigation(Rect rect)
		{
			Event e = Event.current;
			if (!rect.Contains(e.mousePosition))
			{
				return;
			}

			if (e.type == EventType.MouseDrag && e.button == 0)
			{
				orbit.x += e.delta.x;
				orbit.y = Mathf.Clamp(orbit.y + e.delta.y, -80f, 80f);
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && e.button == 2)
			{
				pivotHeight = Mathf.Clamp(pivotHeight + e.delta.y * 0.01f, -2f, 4f);
				e.Use();
			}
			else if (e.type == EventType.ScrollWheel)
			{
				distance = Mathf.Clamp(distance + e.delta.y * 0.1f, 0.8f, 20f);
				e.Use();
			}
		}

		private void Ensure()
		{
			if (preview == null)
			{
				preview = new PreviewRenderUtility();
				preview.camera.fieldOfView = 40f;
				preview.camera.nearClipPlane = 0.05f;
				preview.camera.farClipPlane = 200f;
				preview.camera.clearFlags = CameraClearFlags.SolidColor;
				preview.camera.backgroundColor = new Color(0.18f, 0.18f, 0.19f, 1f);
				preview.lights[0].intensity = 1.1f;
				preview.lights[0].transform.rotation = Quaternion.Euler(40f, 130f, 0f);
				preview.lights[1].intensity = 0.6f;
			}

			if (instance == null && Prefab != null)
			{
				instance = preview.InstantiatePrefabInScene(Prefab);
				instance.transform.position = Vector3.zero;
				instance.transform.rotation = Quaternion.identity;

				// Agent prefabs carry their own cameras and listeners; left enabled they render into the
				// preview scene and fight the preview's own camera.
				foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
				{
					camera.enabled = false;
				}
				foreach (AudioListener listener in instance.GetComponentsInChildren<AudioListener>(true))
				{
					listener.enabled = false;
				}
			}
		}

		/// <summary>Ground reference so a pose's height and facing are readable, as in Unity's own previews.</summary>
		private void DrawGrid()
		{
			if (grid == null)
			{
				grid = BuildGrid(5f, 0.5f);
			}

			if (gridMaterial == null)
			{
				// URP first, since that is what the project renders with; the legacy colored shader is a
				// fallback so this never silently draws magenta.
				Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/Internal-Colored");
				gridMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

				Color color = new Color(1f, 1f, 1f, 0.12f);
				if (gridMaterial.HasProperty("_BaseColor")) gridMaterial.SetColor("_BaseColor", color);
				if (gridMaterial.HasProperty("_Color")) gridMaterial.SetColor("_Color", color);
				if (gridMaterial.HasProperty("_Surface")) gridMaterial.SetFloat("_Surface", 1f); // Transparent
			}

			preview.DrawMesh(grid, Matrix4x4.identity, gridMaterial, 0);
		}

		private static Mesh BuildGrid(float extent, float spacing)
		{
			var vertices = new System.Collections.Generic.List<Vector3>();
			var indices = new System.Collections.Generic.List<int>();

			for (float v = -extent; v <= extent + 0.001f; v += spacing)
			{
				indices.Add(vertices.Count); vertices.Add(new Vector3(v, 0f, -extent));
				indices.Add(vertices.Count); vertices.Add(new Vector3(v, 0f, extent));
				indices.Add(vertices.Count); vertices.Add(new Vector3(-extent, 0f, v));
				indices.Add(vertices.Count); vertices.Add(new Vector3(extent, 0f, v));
			}

			Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
			mesh.SetVertices(vertices);
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		private void PositionCamera()
		{
			Vector3 pivot = new Vector3(0f, pivotHeight, 0f);
			Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
			preview.camera.transform.position = pivot + rotation * new Vector3(0f, 0f, -distance);
			preview.camera.transform.rotation = rotation;
		}

		private void DestroyInstance()
		{
			if (instance != null)
			{
				UnityEngine.Object.DestroyImmediate(instance);
				instance = null;
			}
		}

		#endregion Internals
	}
}
