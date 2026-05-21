using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(ConditionalList))]
	public class ConditionalListDrawer : PropertyDrawer
	{
		private static List<Type> conditionalTypes;

		private static List<Type> GetConditionalTypes()
		{
			if (conditionalTypes != null) return conditionalTypes;
			conditionalTypes = TypeCache.GetTypesDerivedFrom<IConditional>()
				.Where(t => !t.IsAbstract && !typeof(ScriptableObject).IsAssignableFrom(t))
				.OrderBy(t => t.Name)
				.ToList();
			return conditionalTypes;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SerializedProperty listProp = property.FindPropertyRelative("items");
			if (listProp == null) return EditorGUIUtility.singleLineHeight;

			float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			if (!property.isExpanded) return h;

			for (int i = 0; i < listProp.arraySize; i++)
				h += GetElementHeight(listProp.GetArrayElementAtIndex(i)) + EditorGUIUtility.standardVerticalSpacing;

			h += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			return h;
		}

		// Bold type-name header + all child fields rendered inline (no per-element foldout).
		private static float GetElementHeight(SerializedProperty el)
		{
			float h = EditorGUIUtility.singleLineHeight;
			SerializedProperty child = el.Copy();
			SerializedProperty end = el.GetEndProperty();
			bool entered = false;
			while (child.NextVisible(!entered) && !SerializedProperty.EqualContents(child, end))
			{
				entered = true;
				h += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(child, true);
			}
			return h;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty listProp = property.FindPropertyRelative("items");
			if (listProp == null)
			{
				EditorGUI.LabelField(position, label.text, "Missing 'items' field");
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, $"{label.text} [{listProp.arraySize}]", true);

			if (!property.isExpanded)
			{
				EditorGUI.EndProperty();
				return;
			}

			float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
			float removeW = 20f;
			int toRemove = -1;

			EditorGUI.indentLevel++;

			Color separatorColor = EditorGUIUtility.isProSkin
				? new Color(0f, 0f, 0f, 0.35f)
				: new Color(0f, 0f, 0f, 0.15f);

			for (int i = 0; i < listProp.arraySize; i++)
			{
				if (i > 0)
					EditorGUI.DrawRect(new Rect(position.x, y - EditorGUIUtility.standardVerticalSpacing * 0.5f, position.width, 1f), separatorColor);

				SerializedProperty el = listProp.GetArrayElementAtIndex(i);
				float elH = GetElementHeight(el);
				if (DrawElement(new Rect(position.x, y, position.width, elH), el, removeW))
					toRemove = i;
				y += elH + EditorGUIUtility.standardVerticalSpacing;
			}

			EditorGUI.indentLevel--;

			if (toRemove >= 0)
			{
				listProp.DeleteArrayElementAtIndex(toRemove);
				property.serializedObject.ApplyModifiedProperties();
			}

			Rect addRect = new Rect(position.x + EditorGUI.indentLevel * 15f, y, position.width, EditorGUIUtility.singleLineHeight);
			if (GUI.Button(addRect, "Add Condition ▾"))
			{
				List<Type> types = GetConditionalTypes();
				GenericMenu menu = new GenericMenu();
				if (types.Count == 0)
				{
					menu.AddDisabledItem(new GUIContent("No conditions found"));
				}
				else
				{
					// Capture path + SerializedObject so the callback still works after a repaint.
					SerializedObject so = property.serializedObject;
					string listPath = listProp.propertyPath;
					foreach (Type type in types)
					{
						Type captured = type;
						menu.AddItem(new GUIContent(type.Name), false, () =>
						{
							SerializedProperty prop = so.FindProperty(listPath);
							prop.InsertArrayElementAtIndex(prop.arraySize);
							SerializedProperty newEl = prop.GetArrayElementAtIndex(prop.arraySize - 1);
							newEl.managedReferenceValue = Activator.CreateInstance(captured);
							so.ApplyModifiedProperties();
						});
					}
				}
				menu.ShowAsContext();
			}

			EditorGUI.EndProperty();
		}

		// Returns true if the × button was clicked.
		private static bool DrawElement(Rect position, SerializedProperty el, float removeW)
		{
			string typeName = GetTypeName(el.managedReferenceFullTypename);
			EditorGUI.LabelField(
				new Rect(position.x, position.y, position.width - removeW - 2f, EditorGUIUtility.singleLineHeight),
				typeName, EditorStyles.boldLabel);
			bool remove = GUI.Button(
				new Rect(position.xMax - removeW, position.y, removeW, EditorGUIUtility.singleLineHeight), "×");

			float y = position.y + EditorGUIUtility.singleLineHeight;
			float fieldWidth = position.width - removeW - 2f;
			SerializedProperty child = el.Copy();
			SerializedProperty end = el.GetEndProperty();
			bool entered = false;
			while (child.NextVisible(!entered) && !SerializedProperty.EqualContents(child, end))
			{
				entered = true;
				y += EditorGUIUtility.standardVerticalSpacing;
				float childH = EditorGUI.GetPropertyHeight(child, true);
				EditorGUI.PropertyField(new Rect(position.x, y, fieldWidth, childH), child, true);
				y += childH;
			}

			return remove;
		}

		private static string GetTypeName(string managedReferenceFullTypename)
		{
			if (string.IsNullOrEmpty(managedReferenceFullTypename)) return "Unknown";
			string typePart = managedReferenceFullTypename.Split(' ').Last();
			string shortName = typePart.Split('.').Last();
			return ObjectNames.NicifyVariableName(shortName);
		}
	}
}
