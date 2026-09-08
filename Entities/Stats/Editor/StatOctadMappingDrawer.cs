using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(StatOctadMapping))]
	public class StatOctadMappingDrawer : PropertyDrawer
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

			label = new GUIContent(BuildLabel(property, label.text), label.tooltip);

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

		private string BuildLabel(SerializedProperty property, string fallback)
		{
			string from = GetSideName(property, "fromSingleStat", "fromSingle", "fromStatOctad");
			string to = GetSideName(property, "toSingleStat", "toSingle", "toStatOctad");
			if (property.FindPropertyRelative("toSubStats").boolValue)
			{
				string sub = Tail(property.FindPropertyRelative("subStat").stringValue);
				if (!string.IsNullOrEmpty(sub))
				{
					to = $"{to}/{sub}";
				}
			}
			return !string.IsNullOrEmpty(from) ? $"{from} > {to}" : fallback;
		}

		private string GetSideName(SerializedProperty property, string toggleField, string singleField, string octadField)
		{
			bool isSingle = property.FindPropertyRelative(toggleField).boolValue;
			if (isSingle)
			{
				string id = property.FindPropertyRelative(singleField).stringValue;
				return Tail(id);
			}
			else
			{
				var obj = property.FindPropertyRelative(octadField).objectReferenceValue;
				return obj != null ? obj.name : string.Empty;
			}
		}

		private static string Tail(string id)
		{
			if (string.IsNullOrEmpty(id)) return string.Empty;
			int slash = id.LastIndexOf('/');
			return slash >= 0 ? id.Substring(slash + 1) : id;
		}

		private IEnumerable<string> GetVisibleFields(SerializedProperty property)
		{
			// From side
			bool fromSingle = property.FindPropertyRelative("fromSingleStat").boolValue;
			yield return "fromSingleStat";
			yield return fromSingle ? "fromSingle" : "fromStatOctad";
			yield return "sourceBase";

			// To side
			bool toSingle = property.FindPropertyRelative("toSingleStat").boolValue;
			yield return "toSingleStat";
			yield return toSingle ? "toSingle" : "toStatOctad";

			// Sub-stat
			yield return "toSubStats";
			if (property.FindPropertyRelative("toSubStats").boolValue)
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
					yield return "scale";
					yield return "shift";
					break;
				case FormulaType.Saturate:
					yield return "satCeiling";
					yield return "satHalf";
					yield return "satPower";
					yield return "scale";
					yield return "shift";
					break;
				default:
					// LevelToPointsStat (7), LevelToPhysic (8), ExpToLevel (9) and ExpToRank (10):
					// fixed curves, only scale and shift are configurable.
					yield return "scale";
					yield return "shift";
					break;
			}

			yield return "modMethod";
			yield return "operation";
		}
	}
}
