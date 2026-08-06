using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws labeled data as a single row - identifier left, value right - instead of a foldout,
	/// so lists of them read and edit like a table. Both fields keep their own drawers.
	/// </summary>
	public abstract class LabeledDataDrawerBase : PropertyDrawer
	{
		/// <summary>Serialized field drawn in the left column.</summary>
		protected virtual string IdentifierField => "identifier";

		/// <summary>Serialized field drawn in the right column.</summary>
		protected virtual string ValueField => "value";

		/// <summary>Label for the value column; only shows for values that draw one (foldouts).</summary>
		protected virtual GUIContent ValueLabel => GUIContent.none;

		/// <summary>Share of the row width taken by the identifier column.</summary>
		protected virtual float IdentifierShare => 0.5f;

		/// <summary>Left inset per column, needed when a column draws a foldout (see <see cref="FoldoutOffset"/>).</summary>
		protected virtual float ColumnInset => 0f;

		/// <summary>In hierarchy mode a foldout draws its arrow left of its own rect, over whatever sits there.</summary>
		protected static float FoldoutOffset => EditorStyles.foldout.padding.left - EditorStyles.label.padding.left;

		/// <summary>Label share for any nested fields, so their labels stay inside their column.</summary>
		private const float NestedLabelShare = 0.45f;
		private const float Gap = 4f;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return Mathf.Max(EditorGUIUtility.singleLineHeight,
				Height(property, IdentifierField), Height(property, ValueField));
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty identifier = property.FindPropertyRelative(IdentifierField);
			SerializedProperty value = property.FindPropertyRelative(ValueField);
			if (identifier == null || value == null)
			{
				// Renamed field; fall back to the default drawing rather than showing nothing.
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			// List elements drop their "Element 0" label - the identifier is the label there.
			Rect row = IsArrayElement(property) ? position : EditorGUI.PrefixLabel(position, label);

			int indent = EditorGUI.indentLevel;
			float labelWidth = EditorGUIUtility.labelWidth;
			EditorGUI.indentLevel = 0;

			try
			{
				float inset = ColumnInset;
				float idWidth = Mathf.Max(0f, (row.width - Gap) * IdentifierShare);
				float valueWidth = Mathf.Max(0f, row.width - idWidth - Gap);

				// labelWidth is measured from the rect's own x, so scale it per column.
				EditorGUIUtility.labelWidth = Mathf.Max(0f, idWidth - inset) * NestedLabelShare;
				EditorGUI.PropertyField(new Rect(row.x + inset, row.y, Mathf.Max(0f, idWidth - inset),
					EditorGUI.GetPropertyHeight(identifier, true)), identifier, GUIContent.none, true);

				EditorGUIUtility.labelWidth = Mathf.Max(0f, valueWidth - inset) * NestedLabelShare;
				EditorGUI.PropertyField(new Rect(row.xMax - valueWidth + inset, row.y, Mathf.Max(0f, valueWidth - inset),
					EditorGUI.GetPropertyHeight(value, true)), value, ValueLabel, true);
			}
			finally
			{
				EditorGUIUtility.labelWidth = labelWidth;
				EditorGUI.indentLevel = indent;
			}

			EditorGUI.EndProperty();
		}

		private float Height(SerializedProperty property, string field)
		{
			SerializedProperty child = property.FindPropertyRelative(field);
			return child == null ? 0f : EditorGUI.GetPropertyHeight(child, true);
		}

		private static bool IsArrayElement(SerializedProperty property)
		{
			return property.propertyPath.EndsWith("]");
		}
	}

	[CustomPropertyDrawer(typeof(LabeledBoolData))]
	public class LabeledBoolDataDrawer : LabeledDataDrawerBase { }

	[CustomPropertyDrawer(typeof(LabeledFloatData))]
	public class LabeledFloatDataDrawer : LabeledDataDrawerBase { }

	[CustomPropertyDrawer(typeof(LabeledIntData))]
	public class LabeledIntDataDrawer : LabeledDataDrawerBase { }

	[CustomPropertyDrawer(typeof(LabeledStringData))]
	public class LabeledStringDataDrawer : LabeledDataDrawerBase { }

	[CustomPropertyDrawer(typeof(LabeledCurveData))]
	public class LabeledCurveDataDrawer : LabeledDataDrawerBase
	{
		protected override string ValueField => "curve";
	}

	/// <summary>
	/// Octad asset left, <see cref="Vector8"/> right; both stay expandable within their own column.
	/// </summary>
	[CustomPropertyDrawer(typeof(LabeledOctadData))]
	public class LabeledOctadDataDrawer : LabeledDataDrawerBase
	{
		private static readonly GUIContent valuesLabel = new("Values");

		protected override string IdentifierField => "octad";
		protected override string ValueField => "values";
		protected override GUIContent ValueLabel => valuesLabel;

		// Both columns foldout (Expandable asset, Vector8), so both need room for their arrow.
		protected override float ColumnInset => FoldoutOffset;
	}
}
