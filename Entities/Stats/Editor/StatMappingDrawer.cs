using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(StatMapping))]
	public class StatMappingDrawer : PropertyDrawer
	{
		private const float ToggleWidth = 16f;

		private float LineH => EditorGUIUtility.singleLineHeight;
		private float Pad => EditorGUIUtility.standardVerticalSpacing;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded)
				return LineH;

			float h = LineH + Pad;
			foreach (string name in GetVisibleFields(property))
			{
				var prop = property.FindPropertyRelative(name);
				if (prop == null) continue;
				h += EditorGUI.GetPropertyHeight(prop, true) + Pad;
			}
			return h;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			var enabledProp = property.FindPropertyRelative("enabled");
			bool isEnabled = enabledProp.boolValue;

			// -- Header row --
			var foldoutRect = new Rect(position.x, position.y, position.width - ToggleWidth - 2f, LineH);
			var toggleRect = new Rect(position.x + position.width - ToggleWidth, position.y, ToggleWidth, LineH);

			enabledProp.boolValue = EditorGUI.Toggle(toggleRect, isEnabled);

			string from = Tail(property.FindPropertyRelative("fromStat").stringValue);
			string to = Tail(property.FindPropertyRelative("toStat").stringValue);
			if (property.FindPropertyRelative("toSubStat").boolValue)
			{
				string sub = Tail(property.FindPropertyRelative("subStat").stringValue);
				if (!string.IsNullOrEmpty(sub))
				{
					to = $"{to}/{sub}";
				}
			}
			label = new GUIContent(!string.IsNullOrEmpty(from) ? $"{from} > {to}" : label.text, label.tooltip);

			// Grey the label when disabled.
			var prevColor = GUI.contentColor;
			if (!isEnabled)
				GUI.contentColor = new Color(prevColor.r, prevColor.g, prevColor.b, prevColor.a * 0.4f);

			property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

			GUI.contentColor = prevColor;

			// -- Content --
			if (property.isExpanded)
			{
				EditorGUI.indentLevel++;
				float y = position.y + LineH + Pad;

				bool prevEnabled = GUI.enabled;
				GUI.enabled = prevEnabled && isEnabled;

				foreach (string name in GetVisibleFields(property))
				{
					var prop = property.FindPropertyRelative(name);
					if (prop == null) continue;

					float h = EditorGUI.GetPropertyHeight(prop, true);
					EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), prop, true);
					y += h + Pad;
				}

				GUI.enabled = prevEnabled;
				EditorGUI.indentLevel--;
			}

			EditorGUI.EndProperty();
		}

		private static string Tail(string id)
		{
			if (string.IsNullOrEmpty(id)) return string.Empty;
			int slash = id.LastIndexOf('/');
			return slash >= 0 ? id.Substring(slash + 1) : id;
		}

		private IEnumerable<string> GetVisibleFields(SerializedProperty property)
		{
			yield return "fromStat";
			yield return "sourceBase";
			yield return "toStat";
			yield return "toSubStat";

			if (property.FindPropertyRelative("toSubStat").boolValue)
				yield return "subStat";

			yield return "formula";

			switch ((FormulaType)property.FindPropertyRelative("formula").enumValueIndex)
			{
				case FormulaType.Linear:
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Exp:
					yield return "expPreScale";
					yield return "expConstant";
					yield return "expPower";
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.InvExp:
					yield return "invExpPreScale";
					yield return "invExpConstant";
					yield return "invExpPower";
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Log:
					yield return "logConstant";
					yield return "logPower";
					yield return "logShift";
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Curve:
					yield return "curve";
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Interpolate:
					yield return "pointA";
					yield return "pointB";
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Extrapolate:
					yield return "pointA";
					yield return "pointB";
					yield return "shift";
					break;
				// LevelToPointsStat (7) and LevelToPhysic (8): no configurable fields
			}

			yield return "modMethod";
			yield return "operation";
		}
	}
}
