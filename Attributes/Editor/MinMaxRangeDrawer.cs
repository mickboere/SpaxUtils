using UnityEngine;
using UnityEditor;

namespace SpaxUtils
{
	[CustomPropertyDrawer(typeof(MinMaxRangeAttribute))]
	public class MinMaxRangeDrawer : PropertyDrawer
	{
		private const int SPACING = 2;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var attribute = (MinMaxRangeAttribute)base.attribute;
			var propertyType = property.propertyType;

			label.tooltip = $"{attribute.Min} to {attribute.Max}";

			Rect controlRect = EditorGUI.PrefixLabel(position, label);
			Rect[] splitRects = SplitRect(controlRect, 4);

			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			if (propertyType == SerializedPropertyType.Vector2)
			{
				EditorGUI.BeginChangeCheck();

				Vector2 vector = property.vector2Value;
				float minVal = vector.x;
				float maxVal = vector.y;
				minVal = EditorGUI.FloatField(splitRects[0], float.Parse(minVal.ToString("F2")));
				maxVal = EditorGUI.FloatField(splitRects[3], float.Parse(maxVal.ToString("F2")));

				float minExtent = Mathf.Min(minVal, attribute.Min);
				float maxExtent = Mathf.Max(maxVal, attribute.Max);

				Rect sliderRect = new Rect(splitRects[1].position, new Vector2(splitRects[1].width * 2f + SPACING, splitRects[1].height));
				EditorGUI.MinMaxSlider(sliderRect, ref minVal, ref maxVal, minExtent, maxExtent);

				if (attribute.ClampMin)
				{
					minVal = Mathf.Max(minVal, attribute.Min);
				}
				if (attribute.ClampMax)
				{
					maxVal = Mathf.Min(maxVal, attribute.Max);
				}

				vector = new Vector2(minVal > maxVal ? maxVal : minVal, maxVal);

				if (EditorGUI.EndChangeCheck())
				{
					property.vector2Value = vector;
				}
			}
			else if (propertyType == SerializedPropertyType.Vector2Int)
			{
				EditorGUI.BeginChangeCheck();

				Vector2Int vector = property.vector2IntValue;
				float minVal = vector.x;
				float maxVal = vector.y;

				minVal = EditorGUI.FloatField(splitRects[0], minVal);
				maxVal = EditorGUI.FloatField(splitRects[2], maxVal);

				EditorGUI.MinMaxSlider(splitRects[1], ref minVal, ref maxVal,
				attribute.Min, attribute.Max);

				minVal = Mathf.Max(minVal, attribute.Min);
				maxVal = Mathf.Min(maxVal, attribute.Max);

				vector = new Vector2Int(Mathf.FloorToInt(minVal > maxVal ? maxVal : minVal), Mathf.FloorToInt(maxVal));

				if (EditorGUI.EndChangeCheck())
				{
					property.vector2IntValue = vector;
				}
			}

			EditorGUI.indentLevel = indent;
		}

		private Rect[] SplitRect(Rect rectToSplit, int n)
		{
			Rect[] rects = new Rect[n];
			for (int i = 0; i < n; i++)
			{
				rects[i] = new Rect(
					rectToSplit.position.x + rectToSplit.width / n * i + SPACING * (i > 0 ? 1f : 0f),
					rectToSplit.position.y,
					rectToSplit.width / n - SPACING,
					rectToSplit.height);
			}

			return rects;
		}
	}
}
