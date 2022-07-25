using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(CompositeFloat))]
	public class CompositeFloatDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Using BeginProperty / EndProperty on the parent property means that
			// prefab override logic works on the entire property.
			EditorGUI.BeginProperty(position, label, property);

			// Draw label
			position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

			// Don't make child fields be indented
			var indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			// Calculate rects
			Rect defaultValue = new Rect(position.x, position.y, position.width - 145f, position.height);
			Rect minMax = new Rect(position.x + position.width - 140f, position.y, 50f, position.height);
			Rect minValue = new Rect(position.x + position.width - 90f, position.y, 40f, position.height);
			Rect maxValue = new Rect(position.x + position.width - 45f, position.y, 40f, position.height);

			// Draw fields - passs GUIContent.none to each so they are drawn without labels
			EditorGUI.PropertyField(defaultValue, property.FindPropertyRelative("baseValue"), GUIContent.none);
			EditorGUI.LabelField(minMax, "Range:");
			EditorGUI.PropertyField(minValue, property.FindPropertyRelative("minValue"), GUIContent.none);
			EditorGUI.PropertyField(maxValue, property.FindPropertyRelative("maxValue"), GUIContent.none);

			// Set indent back to what it was
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
	}
}