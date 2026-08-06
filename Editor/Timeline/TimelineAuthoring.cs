using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Marker authoring shared by the timeline inspector and the move's phase toggles.
	/// Everything writes through <see cref="SerializedProperty"/>, so edits stay undoable.
	/// </summary>
	public static class TimelineAuthoring
	{
		private const string MARKERS = "markers";
		private const string ID = "id";
		private const string TIME = "time";
		private const string END_MODE = "endMode";
		private const string DURATION = "duration";
		private const string END_MARKER = "endMarker";
		private const string CURVE = "curve";
		private const string DATA = "data";

		/// <summary>
		/// Appends a marker and returns it. Growing the array COPIES the previous element, so every field is
		/// reset - a new marker inheriting the last one's curve or region silently changes how it plays.
		/// </summary>
		public static SerializedProperty AddMarker(SerializedObject timelineObject, string id, float time)
		{
			SerializedProperty markers = timelineObject.FindProperty(MARKERS);
			markers.arraySize++;

			SerializedProperty added = markers.GetArrayElementAtIndex(markers.arraySize - 1);
			added.FindPropertyRelative(ID).stringValue = id;
			added.FindPropertyRelative(TIME).floatValue = time;
			added.FindPropertyRelative(END_MODE).enumValueIndex = (int)MarkerEnd.Point;
			added.FindPropertyRelative(DURATION).floatValue = 0f;
			added.FindPropertyRelative(END_MARKER).stringValue = string.Empty;
			added.FindPropertyRelative(CURVE).animationCurveValue = new AnimationCurve();
			ClearArrays(added.FindPropertyRelative(DATA));
			return added;
		}

		/// <summary>
		/// Adds a phase region, chained to the phase that follows it. Falls back to a point when that phase is
		/// not authored yet, so a charge-only move does not warn about a link it will never have.
		/// </summary>
		public static void AddPhase(SerializedObject timelineObject, AnimationTimeline timeline,
			string id, string chainsTo, float time)
		{
			SerializedProperty added = AddMarker(timelineObject, id, time);
			if (chainsTo != null && timeline.TryGetMarker(chainsTo, out _))
			{
				added.FindPropertyRelative(END_MODE).enumValueIndex = (int)MarkerEnd.Marker;
				added.FindPropertyRelative(END_MARKER).stringValue = chainsTo;
			}
		}

		/// <summary>Removes every marker carrying this identifier. Returns how many went.</summary>
		public static int RemoveMarkers(SerializedObject timelineObject, string id)
		{
			SerializedProperty markers = timelineObject.FindProperty(MARKERS);
			int removed = 0;

			for (int i = markers.arraySize - 1; i >= 0; i--)
			{
				if (markers.GetArrayElementAtIndex(i).FindPropertyRelative(ID).stringValue != id)
				{
					continue;
				}

				markers.DeleteArrayElementAtIndex(i);
				removed++;
			}

			return removed;
		}

		/// <summary>Empties every list nested in a property, leaving its scalars alone.</summary>
		private static void ClearArrays(SerializedProperty property)
		{
			if (property == null)
			{
				return;
			}

			SerializedProperty child = property.Copy();
			SerializedProperty end = property.GetEndProperty();

			bool enterChildren = true;
			while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
			{
				enterChildren = false;
				if (child.propertyType == SerializedPropertyType.Generic && child.isArray)
				{
					child.arraySize = 0;
				}
			}
		}
	}
}
