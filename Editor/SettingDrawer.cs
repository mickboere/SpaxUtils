using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws a <see cref="Setting"/> as the plain field it wraps: its serialized default, ranged when the attribute has a range.
	/// </summary>
	[CustomPropertyDrawer(typeof(Setting), true)]
	public class SettingDrawer : PropertyDrawer
	{
		private const string DEFAULT_VALUE = "defaultValue";

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SerializedProperty value = property.FindPropertyRelative(DEFAULT_VALUE);
			return value == null || IsRuntimeOnly() ?
				EditorGUIUtility.singleLineHeight :
				EditorGUI.GetPropertyHeight(value, label, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SettingAttribute info = fieldInfo?.GetCustomAttribute<SettingAttribute>();
			if (info != null && string.IsNullOrEmpty(label.tooltip))
			{
				label.tooltip = $"{info.Tab}/{info.Section}: {info.Key}";
			}

			SerializedProperty value = property.FindPropertyRelative(DEFAULT_VALUE);
			if (value == null || IsRuntimeOnly())
			{
				EditorGUI.LabelField(position, label, new GUIContent("Set in-game"));
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();
			if (info != null && info.HasRange && value.propertyType == SerializedPropertyType.Float)
			{
				EditorGUI.Slider(position, value, info.Min, info.Max, label);
			}
			else if (info != null && info.HasRange && value.propertyType == SerializedPropertyType.Integer)
			{
				EditorGUI.IntSlider(position, value, (int)info.Min, (int)info.Max, label);
			}
			else
			{
				EditorGUI.PropertyField(position, value, label, true);
			}
			if (EditorGUI.EndChangeCheck() && Application.isPlaying)
			{
				property.serializedObject.ApplyModifiedProperties();
				SyncRunningCopy(property.serializedObject.targetObject);
			}
			EditorGUI.EndProperty();
		}

		/// <summary>
		/// Services run on an instantiated copy, so push the edited default over to it.
		/// </summary>
		private void SyncRunningCopy(Object asset)
		{
			if (!GlobalDependencyManager.HasInstance || fieldInfo == null ||
				!fieldInfo.DeclaringType.IsAssignableFrom(asset.GetType()))
			{
				return;
			}

			object running = GlobalDependencyManager.Instance.Get(asset.GetType(), asset.GetType(), true, false);
			if (running != null && !ReferenceEquals(running, asset) &&
				fieldInfo.GetValue(running) is Setting target && fieldInfo.GetValue(asset) is Setting source)
			{
				target.SyncDefault(source);
			}
		}

		private bool IsRuntimeOnly()
		{
			return fieldInfo != null && typeof(BindingsSetting).IsAssignableFrom(fieldInfo.FieldType);
		}
	}
}
