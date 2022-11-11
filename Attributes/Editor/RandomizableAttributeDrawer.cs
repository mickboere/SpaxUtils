using UnityEngine;
using UnityEditor;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(RandomizableAttribute))]
	public class RandomizableAttributeDrawer : PropertyDrawer
	{
		private const float buttonWidth = 80;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			position.width -= buttonWidth;

			EditorGUI.PropertyField(position, property, label, true);

			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;
			position.x += position.width;
			position.width = buttonWidth;
			Button(position, property);
			EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}
		private void Button(Rect position, SerializedProperty property)
		{
			if (GUI.Button(position, "Randomize"))
			{
				property.intValue = RandomService.GenerateSeed();
			}
		}

		// Cannot figure out how to retrieve method from nested property, so commenting it out until figure it out.
		// https://forum.unity.com/threads/attribute-to-add-button-to-class.660262/
		//private void Button(Rect position, SerializedProperty property)
		//{
		//	ButtonAttribute buttonAttribute = attribute as ButtonAttribute;
		//	string methodName = buttonAttribute.Method;

		//	Object target = property.serializedObject.targetObject;
		//	System.Type type = target.GetType();
		//	System.Reflection.MethodInfo method = type.GetMethod(methodName);

		//	if (method == null)
		//	{
		//		GUI.color = Color.red;
		//		GUI.Label(position, "Method could not be found. Is it public?");
		//		return;
		//	}
		//	else if (method.GetParameters().Length > 0)
		//	{
		//		GUI.color = Color.red;
		//		GUI.Label(position, "Method cannot have parameters.");
		//		return;
		//	}
		//	if (GUI.Button(position, buttonAttribute.Text))
		//	{
		//		method.Invoke(target, null);
		//	}
		//}
	}
}
