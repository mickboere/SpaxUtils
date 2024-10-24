﻿using UnityEngine;
using UnityEditor;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ConditionalAttribute))]
	public class ConditionalAttributeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			ConditionalAttribute conditionalAttribute = attribute as ConditionalAttribute;
			SerializedProperty toggleProperty = property.FindNeighbourProperty(conditionalAttribute.ToggleProperty);

			if (conditionalAttribute.EnumValue > -1)
			{
				// Enum
				PropertyIsEnum(position, property, label, conditionalAttribute, toggleProperty);
			}
			else
			{
				// Bool
				PropertyIsBool(position, property, label, conditionalAttribute, toggleProperty);
			}
		}

		private void PropertyIsEnum(Rect position, SerializedProperty property, GUIContent label,
			ConditionalAttribute conditionalAttribute, SerializedProperty toggleProperty)
		{
			if (toggleProperty.enumValueIndex == conditionalAttribute.EnumValue == conditionalAttribute.Inverse)
			{
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.PropertyField(position, property, label, true);
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

			if ((conditionalAttribute.EnumValue > -1 && toggleProperty.enumValueIndex == conditionalAttribute.EnumValue == conditionalAttribute.Inverse) ||
				(conditionalAttribute.Hide && toggleProperty.boolValue == conditionalAttribute.Inverse))
			{
				return 0f;
			}

			return base.GetPropertyHeight(property, label);
		}
	}
}
