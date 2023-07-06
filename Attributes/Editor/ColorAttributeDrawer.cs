using UnityEngine;
using UnityEditor;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ColorAttribute))]
	public class ColorAttributeDrawer : PropertyDrawer
	{
		ColorAttribute attr => (ColorAttribute)attribute;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			Color originalColor = GUI.color;
			Color newColor = originalColor;

			if (string.IsNullOrWhiteSpace(attr.colorProperty))
			{
				newColor = attr.color;
			}
			else
			{
				SerializedProperty colorProperty = property.FindProperty(attr.colorProperty);
				if (colorProperty != null)
				{
					newColor = colorProperty.colorValue;
				}
			}

			label.text = newColor.RichWrap(label.text);
			EditorGUI.PropertyField(position, property, label, true);

			//GUI.color = originalColor;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUI.GetPropertyHeight(property, label, true);
		}
	}
}