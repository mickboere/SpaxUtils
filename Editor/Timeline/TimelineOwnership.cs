using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Whether a <see cref="PerformanceMove"/> owns its <see cref="AnimationTimeline"/> as a child asset or
	/// references a shared standalone one. Ownership is DERIVED from where the asset lives, never stored.
	/// </summary>
	public static class TimelineOwnership
	{
		/// <summary>Backing field on <see cref="PerformanceMove"/>; the one place this name is written down.</summary>
		public const string TIMELINE_FIELD = "timeline";

		private const string CHILD_NAME = "Timeline";

		/// <summary>Whether this timeline is a child asset of the move, rather than a shared standalone one.</summary>
		public static bool IsOwned(PerformanceMove move, AnimationTimeline timeline)
		{
			if (move == null || timeline == null)
			{
				return false;
			}

			// Same file but not the file's main asset is exactly what a child asset is.
			string path = AssetDatabase.GetAssetPath(move);
			return !string.IsNullOrEmpty(path) &&
				path == AssetDatabase.GetAssetPath(timeline) &&
				!AssetDatabase.IsMainAsset(timeline);
		}

		/// <summary>Creates a timeline owned by the move and assigns it.</summary>
		public static AnimationTimeline Create(PerformanceMove move)
		{
			if (!AssetDatabase.Contains(move))
			{
				Debug.LogError($"[TimelineOwnership] '{move.name}' is not a saved asset, so it cannot own a timeline.");
				return null;
			}

			AnimationTimeline timeline = ScriptableObject.CreateInstance<AnimationTimeline>();
			timeline.name = CHILD_NAME;
			AssetDatabase.AddObjectToAsset(timeline, move);
			Assign(move, timeline);
			AssetDatabase.SaveAssets();
			return timeline;
		}

		/// <summary>
		/// Copies a standalone timeline into the move as a child and assigns the copy.
		/// Deliberately non-destructive: the source asset is left alone for the author to delete or keep sharing.
		/// </summary>
		public static AnimationTimeline Embed(PerformanceMove move, AnimationTimeline source)
		{
			if (source == null || !AssetDatabase.Contains(move))
			{
				return null;
			}

			AnimationTimeline copy = Object.Instantiate(source);
			copy.name = CHILD_NAME;
			AssetDatabase.AddObjectToAsset(copy, move);
			Assign(move, copy);
			AssetDatabase.SaveAssets();
			return copy;
		}

		/// <summary>Promotes an owned timeline into a standalone asset so other moves can share it.</summary>
		public static bool Extract(PerformanceMove move, AnimationTimeline timeline)
		{
			if (!IsOwned(move, timeline))
			{
				return false;
			}

			string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(move)).Replace("\\", "/");
			string path = EditorUtility.SaveFilePanelInProject("Extract Timeline",
				$"Timeline_{move.name}", "asset", "Promote this move's timeline into a shareable asset.", folder);
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			AssetDatabase.RemoveObjectFromAsset(timeline);
			AssetDatabase.CreateAsset(timeline, path);

			// Re-point explicitly rather than trusting the reference to follow the object to its new GUID.
			Assign(move, timeline);
			AssetDatabase.SaveAssets();
			return true;
		}

		/// <summary>
		/// Destroys an owned timeline and clears the reference. Refuses a shared standalone - unassigning one
		/// move must never delete an asset other moves are still using.
		/// </summary>
		public static bool Delete(PerformanceMove move, AnimationTimeline timeline)
		{
			if (!IsOwned(move, timeline))
			{
				return false;
			}

			if (!EditorUtility.DisplayDialog("Delete Timeline",
				$"Delete the timeline owned by '{move.name}'?\n\nIts markers go with it. The clip is a separate asset and is left alone.",
				"Delete", "Cancel"))
			{
				return false;
			}

			Assign(move, null);
			AssetDatabase.RemoveObjectFromAsset(timeline);
			Object.DestroyImmediate(timeline, true);
			AssetDatabase.SaveAssets();
			return true;
		}

		/// <summary>
		/// The timeline conversion should write into: whatever is already assigned, else a fresh child asset.
		/// Keeps a bulk migration from scattering standalone assets that then need re-homing.
		/// </summary>
		public static AnimationTimeline GetOrCreate(PerformanceMove move)
		{
			return move.Timeline != null ? move.Timeline : Create(move);
		}

		/// <summary>
		/// Writes the move's timeline reference. Callers holding their own <see cref="SerializedObject"/> for the
		/// move must Update() afterwards, or their next Apply will write this back out.
		/// </summary>
		public static void Assign(PerformanceMove move, AnimationTimeline timeline)
		{
			SerializedObject serialized = new SerializedObject(move);
			SerializedProperty property = serialized.FindProperty(TIMELINE_FIELD);
			if (property == null)
			{
				Debug.LogError($"[TimelineOwnership] '{TIMELINE_FIELD}' not found on {nameof(PerformanceMove)}.");
				return;
			}

			property.objectReferenceValue = timeline;
			serialized.ApplyModifiedProperties();
			EditorUtility.SetDirty(move);
		}
	}
}
