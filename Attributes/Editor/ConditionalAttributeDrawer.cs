using UnityEngine;
using UnityEditor;
using System.Linq;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ConditionalAttribute))]
	public class ConditionalAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			ConditionalAttribute conditionalAttribute = attribute as ConditionalAttribute;
			SerializedProperty toggleProperty = property.FindNeighbourProperty(conditionalAttribute.ToggleProperty);

			if (conditionalAttribute.EnumValues.Length > 0)
			{
				// Enum
				PropertyIsEnum(position, property, label, conditionalAttribute, toggleProperty);
			}
			else if (toggleProperty.propertyType == SerializedPropertyType.String)
			{
				// String (truthy = non-empty; inverse to show when empty)
				PropertyIsString(position, property, label, conditionalAttribute, toggleProperty);
			}
			else
			{
				// Bool
				PropertyIsBool(position, property, label, conditionalAttribute, toggleProperty);
			}
		}

		private void PropertyIsString(Rect position, SerializedProperty property, GUIContent label,
			ConditionalAttribute conditionalAttribute, SerializedProperty toggleProperty)
		{
			bool toggle = !string.IsNullOrEmpty(toggleProperty.stringValue);
			if (conditionalAttribute.Hide && toggle == conditionalAttribute.Inverse)
			{
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginDisabledGroup(toggle == conditionalAttribute.Inverse);
			EditorGUI.PropertyField(position, property, label, true);
			EditorGUI.EndDisabledGroup();
			EditorGUI.EndProperty();
		}

		private void PropertyIsEnum(Rect position, SerializedProperty property, GUIContent label,
			ConditionalAttribute conditionalAttribute, SerializedProperty toggleProperty)
		{
			bool valid = conditionalAttribute.EnumValues.Contains(toggleProperty.enumValueIndex) != conditionalAttribute.Inverse;
			if (conditionalAttribute.Hide && !valid)
			{
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginDisabledGroup(!valid);
			EditorGUI.PropertyField(position, property, label, true);
			EditorGUI.EndDisabledGroup();
			EditorGUI.EndProperty();
		}

		private void PropertyIsBool(Rect position, SerializedProperty property, GUIContent label,
			ConditionalAttribute conditionalAttribute, SerializedProperty toggleProperty)
		{
			if (conditionalAttribute.Hide && toggleProperty.boolValue == conditionalAttribute.Inverse)
			{
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			if (conditionalAttribute.DrawToggle)
			{
				position.width -= 24;
			}
			EditorGUI.BeginDisabledGroup(toggleProperty.boolValue == conditionalAttribute.Inverse);
			EditorGUI.PropertyField(position, property, label, true);
			EditorGUI.EndDisabledGroup();

			if (conditionalAttribute.DrawToggle)
			{
				int indent = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
				position.x += position.width + 24;
				position.width = position.height = EditorGUI.GetPropertyHeight(toggleProperty);
				position.x -= position.width;
				EditorGUI.PropertyField(position, toggleProperty, GUIContent.none);
				EditorGUI.indentLevel = indent;
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			ConditionalAttribute conditionalAttribute = attribute as ConditionalAttribute;
			SerializedProperty toggleProperty = property.FindNeighbourProperty(conditionalAttribute.ToggleProperty);

			bool hidden;
			if (conditionalAttribute.EnumValues.Length > 0)
			{
				hidden = conditionalAttribute.EnumValues.Contains(toggleProperty.enumValueIndex) == conditionalAttribute.Inverse;
			}
			else if (toggleProperty.propertyType == SerializedPropertyType.String)
			{
				hidden = !string.IsNullOrEmpty(toggleProperty.stringValue) == conditionalAttribute.Inverse;
			}
			else
			{
				hidden = toggleProperty.boolValue == conditionalAttribute.Inverse;
			}

			if (conditionalAttribute.Hide && hidden)
			{
				return 0f;
			}

			return EditorGUI.GetPropertyHeight(property, label);
		}
	}
}
