// This file must be placed inside an Editor folder (e.g. Assets/SpaxUtils/Editor/).
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SpaxUtils
{
	[CustomEditor(typeof(WorldRegion))]
	public class WorldRegionEditor : Editor
	{
		private static readonly Color SELECTED_COLOR = new Color(0.3f, 0.7f, 1f, 1f);
		private static readonly Color HANDLE_COLOR = new Color(0.3f, 0.7f, 1f, 0.9f);

		private WorldRegion worldRegion;
		private int selectedRegionIndex = -1;
		private bool regionsFoldout = true;

		private SerializedProperty prioProperty;
		private SerializedProperty regionsProperty;
		private SerializedProperty gizmosColorProperty;
		private SerializedProperty alwaysDrawGizmosProperty;
		private SerializedProperty autoCollectPoisProperty;
		private SerializedProperty poisProperty;

		private void OnEnable()
		{
			worldRegion = (WorldRegion)target;
			prioProperty = serializedObject.FindProperty("prio");
			regionsProperty = serializedObject.FindProperty("regions");
			gizmosColorProperty = serializedObject.FindProperty("gizmosColor");
			alwaysDrawGizmosProperty = serializedObject.FindProperty("alwaysDrawGizmos");
			autoCollectPoisProperty = serializedObject.FindProperty("autoCollectPois");
			poisProperty = serializedObject.FindProperty("pois");

			// Ensure cache is populated in edit mode (Awake does not run outside play mode).
			worldRegion.BuildCache();
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(prioProperty);
			EditorGUILayout.PropertyField(gizmosColorProperty);
			EditorGUILayout.PropertyField(alwaysDrawGizmosProperty);

			EditorGUILayout.Space(6f);
			DrawRegionsList();

			EditorGUILayout.Space(6f);
			EditorGUILayout.PropertyField(autoCollectPoisProperty);
			EditorGUILayout.PropertyField(poisProperty);

			serializedObject.ApplyModifiedProperties();
		}

		// -------------------------------------------------------------------------
		// Inspector - Regions List
		// -------------------------------------------------------------------------

		private void DrawRegionsList()
		{
			regionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(regionsFoldout, $"Regions ({regionsProperty.arraySize})");

			if (regionsFoldout)
			{
				EditorGUI.indentLevel++;

				for (int i = 0; i < regionsProperty.arraySize; i++)
				{
					DrawRegionInspector(i);
				}

				EditorGUI.indentLevel--;

				if (GUILayout.Button("Add Region"))
				{
					regionsProperty.InsertArrayElementAtIndex(regionsProperty.arraySize);
					selectedRegionIndex = regionsProperty.arraySize - 1;
					SceneView.RepaintAll();
				}
			}

			EditorGUILayout.EndFoldoutHeaderGroup();
		}

		private void DrawRegionInspector(int index)
		{
			SerializedProperty regionProp = regionsProperty.GetArrayElementAtIndex(index);
			SerializedProperty typeProp = regionProp.FindPropertyRelative("type");
			SerializedProperty offsetProp = regionProp.FindPropertyRelative("offset");
			SerializedProperty rotProp = regionProp.FindPropertyRelative("rotation");
			SerializedProperty boxSizeProp = regionProp.FindPropertyRelative("boxSize");
			SerializedProperty radiusProp = regionProp.FindPropertyRelative("radius");

			bool isSelected = selectedRegionIndex == index;
			WorldRegion.RegionType type = (WorldRegion.RegionType)typeProp.enumValueIndex;

			// Header row: foldout + remove button.
			EditorGUILayout.BeginHorizontal();

			GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
			if (isSelected)
			{
				foldoutStyle.normal.textColor = SELECTED_COLOR;
				foldoutStyle.focused.textColor = SELECTED_COLOR;
				foldoutStyle.onNormal.textColor = SELECTED_COLOR;
			}

			bool foldoutOpen = EditorGUILayout.Foldout(isSelected, $"Region {index}  [{type}]", true, foldoutStyle);
			if (foldoutOpen != isSelected)
			{
				selectedRegionIndex = foldoutOpen ? index : -1;
				SceneView.RepaintAll();
			}

			if (GUILayout.Button("X", GUILayout.Width(22f)))
			{
				regionsProperty.DeleteArrayElementAtIndex(index);
				if (selectedRegionIndex == index)
				{
					selectedRegionIndex = -1;
				}
				else if (selectedRegionIndex > index)
				{
					selectedRegionIndex--;
				}
				serializedObject.ApplyModifiedProperties();
				SceneView.RepaintAll();
				return;
			}

			EditorGUILayout.EndHorizontal();

			if (!isSelected)
			{
				return;
			}

			EditorGUI.indentLevel++;

			EditorGUILayout.PropertyField(typeProp);
			EditorGUILayout.PropertyField(offsetProp);

			if (type == WorldRegion.RegionType.Box)
			{
				EditorGUILayout.PropertyField(rotProp, new GUIContent("Rotation"));
				EditorGUILayout.PropertyField(boxSizeProp, new GUIContent("Box Size"));
			}
			else
			{
				// Show rotation greyed-out for spheres so the value is preserved on type change.
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.PropertyField(rotProp, new GUIContent("Rotation (Box only)"));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.PropertyField(radiusProp, new GUIContent("Radius"));
			}

			EditorGUI.indentLevel--;
		}

		// -------------------------------------------------------------------------
		// Scene GUI
		// -------------------------------------------------------------------------

		private void OnSceneGUI()
		{
			if (worldRegion == null)
			{
				return;
			}

			serializedObject.Update();

			for (int i = 0; i < regionsProperty.arraySize; i++)
			{
				DrawRegionSceneHandles(i);
			}

			serializedObject.ApplyModifiedProperties();

			// Rebuild runtime cache so gizmos stay in sync while authoring in the scene view.
			worldRegion.BuildCache();
		}

		private void DrawRegionSceneHandles(int index)
		{
			SerializedProperty regionProp = regionsProperty.GetArrayElementAtIndex(index);
			SerializedProperty typeProp = regionProp.FindPropertyRelative("type");
			SerializedProperty offsetProp = regionProp.FindPropertyRelative("offset");
			SerializedProperty rotProp = regionProp.FindPropertyRelative("rotation");
			SerializedProperty boxSizeProp = regionProp.FindPropertyRelative("boxSize");
			SerializedProperty radiusProp = regionProp.FindPropertyRelative("radius");

			WorldRegion.RegionType type = (WorldRegion.RegionType)typeProp.enumValueIndex;
			Vector3 worldCenter = worldRegion.GetWorldCenter(index);
			Quaternion fullRotation = worldRegion.GetWorldRotation(index);

			bool isSelected = selectedRegionIndex == index;

			// Clickable dot at region center.
			float dotSize = HandleUtility.GetHandleSize(worldCenter) * 0.04f;
			Handles.color = isSelected ? SELECTED_COLOR : Color.white;

			if (Handles.Button(worldCenter, Quaternion.identity, dotSize, dotSize * 1.5f, Handles.DotHandleCap))
			{
				selectedRegionIndex = index;
				Repaint();
			}

			if (!isSelected)
			{
				return;
			}

			Handles.color = HANDLE_COLOR;

			// Position handle.
			EditorGUI.BeginChangeCheck();
			Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, fullRotation);
			if (EditorGUI.EndChangeCheck())
			{
				offsetProp.vector3Value = worldRegion.transform.InverseTransformPoint(newWorldCenter);
			}

			if (type == WorldRegion.RegionType.Box)
			{
				// Rotation handle.
				EditorGUI.BeginChangeCheck();
				Quaternion newWorldRotation = Handles.RotationHandle(fullRotation, worldCenter);
				if (EditorGUI.EndChangeCheck())
				{
					Quaternion localRotation = Quaternion.Inverse(worldRegion.transform.rotation) * newWorldRotation;
					rotProp.vector3Value = localRotation.eulerAngles;
				}

				// Six one-sided face handles.
				DrawBoxFaceHandles(offsetProp, rotProp, boxSizeProp, worldCenter, fullRotation);
			}
			else
			{
				// Radius handle.
				EditorGUI.BeginChangeCheck();
				float newRadius = Handles.RadiusHandle(Quaternion.identity, worldCenter, radiusProp.floatValue);
				if (EditorGUI.EndChangeCheck())
				{
					radiusProp.floatValue = Mathf.Max(0.01f, newRadius);
				}
			}
		}

		/// <summary>
		/// Draws six face slider handles for a box region.
		/// Moving a face only changes size and shifts the center toward that face (one-sided).
		/// </summary>
		private void DrawBoxFaceHandles(
			SerializedProperty offsetProp,
			SerializedProperty rotProp,
			SerializedProperty boxSizeProp,
			Vector3 worldCenter,
			Quaternion fullRotation)
		{
			// Local-space face directions (one per face).
			Vector3[] localFaceDirs = new Vector3[]
			{
				Vector3.right,   Vector3.left,
				Vector3.up,      Vector3.down,
				Vector3.forward, Vector3.back
			};

			Vector3 boxSize = boxSizeProp.vector3Value;
			Vector3 currentOffset = offsetProp.vector3Value;
			Vector3 regionRotEuler = rotProp.vector3Value;

			foreach (Vector3 localFaceDir in localFaceDirs)
			{
				Vector3 worldFaceDir = fullRotation * localFaceDir;

				// Half-extent from center to this face in world units.
				float halfSize =
					Mathf.Abs(localFaceDir.x) * boxSize.x * 0.5f +
					Mathf.Abs(localFaceDir.y) * boxSize.y * 0.5f +
					Mathf.Abs(localFaceDir.z) * boxSize.z * 0.5f;

				Vector3 faceWorldPos = worldCenter + worldFaceDir * halfSize;
				float handleSize = HandleUtility.GetHandleSize(faceWorldPos) * 0.06f;

				EditorGUI.BeginChangeCheck();
				Vector3 newFacePos = Handles.Slider(faceWorldPos, worldFaceDir, handleSize, Handles.DotHandleCap, 0f);
				if (EditorGUI.EndChangeCheck())
				{
					// How far the face moved along its outward normal.
					float delta = Vector3.Dot(newFacePos - faceWorldPos, worldFaceDir);

					// Grow or shrink the size along the affected axis only.
					Vector3 newSize = boxSize;
					newSize.x = Mathf.Max(0.01f, boxSize.x + Mathf.Abs(localFaceDir.x) * delta);
					newSize.y = Mathf.Max(0.01f, boxSize.y + Mathf.Abs(localFaceDir.y) * delta);
					newSize.z = Mathf.Max(0.01f, boxSize.z + Mathf.Abs(localFaceDir.z) * delta);
					boxSizeProp.vector3Value = newSize;

					// Shift the region center by half the delta in the face direction.
					// The shift is in region-local space; rotate it into WorldRegion-local space.
					Vector3 regionLocalShift = localFaceDir * delta * 0.5f;
					Vector3 worldRegionLocalShift = Quaternion.Euler(regionRotEuler) * regionLocalShift;
					offsetProp.vector3Value = currentOffset + worldRegionLocalShift;
				}
			}
		}
	}
}
