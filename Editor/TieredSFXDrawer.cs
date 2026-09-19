using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws a <see cref="TieredSFX"/> as shared settings plus a tier ladder, each tier able to override them.
	/// </summary>
	[CustomPropertyDrawer(typeof(TieredSFX))]
	public class TieredSFXDrawer : PropertyDrawer
	{
		private const float PAD = 2f;
		private const float TOGGLE_WIDTH = 74f;

		private Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float line = EditorGUIUtility.singleLineHeight;

			if (!property.isExpanded)
			{
				return line;
			}

			float height = (line + PAD) * 2f;
			height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("defaults"), true) + PAD;
			height += GetList(property).GetHeight() + PAD;
			return height;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			float line = EditorGUIUtility.singleLineHeight;
			Rect rect = new Rect(position.x, position.y, position.width, line);

			property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
			if (!property.isExpanded)
			{
				return;
			}

			EditorGUI.indentLevel++;

			rect.y += line + PAD;
			EditorGUI.PropertyField(rect, property.FindPropertyRelative("mode"));

			SerializedProperty defaults = property.FindPropertyRelative("defaults");
			rect.y += line + PAD;
			rect.height = EditorGUI.GetPropertyHeight(defaults, true);
			EditorGUI.PropertyField(rect, defaults, new GUIContent("Shared Settings"), true);

			EditorGUI.indentLevel--;

			ReorderableList list = GetList(property);
			rect.y += rect.height + PAD;
			rect.height = list.GetHeight();
			list.DoList(EditorGUI.IndentedRect(rect));
		}

		private ReorderableList GetList(SerializedProperty property)
		{
			SerializedProperty tiers = property.FindPropertyRelative("tiers");
			string key = property.serializedObject.targetObject.GetInstanceID() + property.propertyPath;

			if (!lists.TryGetValue(key, out ReorderableList list))
			{
				list = new ReorderableList(property.serializedObject, tiers, true, true, true, true);
				list.drawHeaderCallback = header => EditorGUI.LabelField(header, "Tiers");
				lists[key] = list;
			}

			// Only the list's UI state is cached; its property and callbacks would otherwise go stale.
			list.serializedProperty = tiers;
			list.elementHeightCallback = index => ElementHeight(tiers, index);
			list.drawElementCallback = (rect, index, active, focused) => DrawElement(rect, tiers, index);
			list.onAddCallback = target => AddTier(property, target);
			return list;
		}

		private float ElementHeight(SerializedProperty tiers, int index)
		{
			SerializedProperty tier = tiers.GetArrayElementAtIndex(index);
			float height = EditorGUIUtility.singleLineHeight + PAD * 2f;

			height += EditorGUI.GetPropertyHeight(tier.FindPropertyRelative("clips"), true) + PAD;

			if (tier.FindPropertyRelative("overrideSettings").boolValue)
			{
				height += EditorGUI.GetPropertyHeight(tier.FindPropertyRelative("settings"), true) + PAD;
			}

			return height;
		}

		private void DrawElement(Rect rect, SerializedProperty tiers, int index)
		{
			SerializedProperty tier = tiers.GetArrayElementAtIndex(index);
			SerializedProperty clips = tier.FindPropertyRelative("clips");
			SerializedProperty overrides = tier.FindPropertyRelative("overrideSettings");
			SerializedProperty settings = tier.FindPropertyRelative("settings");

			Rect row = new Rect(rect.x, rect.y + PAD, rect.width - TOGGLE_WIDTH - PAD,
				EditorGUIUtility.singleLineHeight);
			EditorGUI.PropertyField(row, tier.FindPropertyRelative("intensity"), new GUIContent("Intensity"));

			row.x = rect.xMax - TOGGLE_WIDTH;
			row.width = TOGGLE_WIDTH;
			overrides.boolValue = EditorGUI.ToggleLeft(row, "Override", overrides.boolValue);

			row = new Rect(rect.x, row.yMax + PAD, rect.width, EditorGUI.GetPropertyHeight(clips, true));
			EditorGUI.PropertyField(row, clips, true);

			if (!overrides.boolValue)
			{
				return;
			}

			row = new Rect(rect.x, row.yMax + PAD, rect.width, EditorGUI.GetPropertyHeight(settings, true));
			EditorGUI.PropertyField(row, settings, new GUIContent("Settings"), true);
		}

		private void AddTier(SerializedProperty property, ReorderableList list)
		{
			SerializedProperty tiers = list.serializedProperty;
			int index = tiers.arraySize;
			tiers.InsertArrayElementAtIndex(index);

			SerializedProperty tier = tiers.GetArrayElementAtIndex(index);
			tier.FindPropertyRelative("clips").ClearArray();
			tier.FindPropertyRelative("overrideSettings").boolValue = false;
			tier.FindPropertyRelative("intensity").floatValue = index == 0 ? 0f : 1f;

			// Seed the override block from the shared settings, so overriding starts where they left off.
			SerializedProperty defaults = property.FindPropertyRelative("defaults");
			SerializedProperty settings = tier.FindPropertyRelative("settings");
			settings.FindPropertyRelative("volumeRange").vector2Value =
				defaults.FindPropertyRelative("volumeRange").vector2Value;
			settings.FindPropertyRelative("pitchRange").vector2Value =
				defaults.FindPropertyRelative("pitchRange").vector2Value;
			settings.FindPropertyRelative("distance").floatValue =
				defaults.FindPropertyRelative("distance").floatValue;

			list.index = index;
		}
	}
}
