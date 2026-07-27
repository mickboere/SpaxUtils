using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws an octad's 8 members labeled in their element color, in octad order (N, NE, E, SE, S, SW, W, NW).
	/// Members keep their own drawers, so Range/MinMaxRange/ConstDropdown all still apply.
	/// </summary>
	public abstract class OctadDrawerBase : PropertyDrawer
	{
		/// <summary>
		/// backgroundColor MULTIPLIES the control texture: below 1 darkens it (saturated but dim), above 1 brightens.
		/// Controls that draw TEXT on their background (dropdowns, number fields) need it at or under 1, else the
		/// button closes on the tinted text and both blur together. A slider track carries no text, so it can be
		/// overdriven into full color - see <see cref="RangedOctadDrawer"/>.
		/// </summary>
		protected virtual float BackgroundBoost => 1f;

		private static readonly Dictionary<string, string[]> assetLabels = new();
		private OctadLabelsAttribute labelsAttribute;
		private bool labelsResolved;

		/// <summary>
		/// contentColor MULTIPLIES the style's text color, and the dark skin's label grey is only ~0.77 - so a
		/// raw tint lands ~30% darker than authored. Pre-divide by that grey to put the element color back on screen.
		/// </summary>
		private const float ContentBoost = 1f / 0.77f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			Rect header = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);

			if (property.isExpanded)
			{
				EditorGUI.indentLevel++;
				Color content = GUI.contentColor;
				Color background = GUI.backgroundColor;

				try
				{
					float y = header.yMax + EditorGUIUtility.standardVerticalSpacing;
					int index = 0;
					foreach (SerializedProperty member in Members(property))
					{
						Tint(index);
						float height = EditorGUI.GetPropertyHeight(member, true);
						DrawMember(new Rect(position.x, y, position.width, height), member, Label(member, index), index);
						y += height + EditorGUIUtility.standardVerticalSpacing;
						index++;
					}
				}
				finally
				{
					GUI.contentColor = content;
					GUI.backgroundColor = background;
					EditorGUI.indentLevel--;
				}
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = EditorGUIUtility.singleLineHeight;
			if (!property.isExpanded)
			{
				return height;
			}

			foreach (SerializedProperty member in Members(property))
			{
				height += EditorGUI.GetPropertyHeight(member, true) + EditorGUIUtility.standardVerticalSpacing;
			}
			return height;
		}

		/// <summary>Draws one member row. Override to replace the default control with a custom one.</summary>
		protected virtual void DrawMember(Rect rect, SerializedProperty member, GUIContent label, int index)
		{
			EditorGUI.PropertyField(rect, member, label, true);
		}

		/// <summary>
		/// Tints the whole row in the element color: label and value text via contentColor, field boxes and
		/// slider tracks via backgroundColor. Backgrounds get a softened tint so the row stays readable.
		/// </summary>
		private void Tint(int index)
		{
			if (index >= 8)
			{
				GUI.contentColor = Color.white;
				GUI.backgroundColor = Color.white;
				return;
			}

			Color color = ElementColors.Get(index);
			GUI.contentColor = color * ContentBoost;
			GUI.backgroundColor = color * BackgroundBoost;
		}

		/// <summary>
		/// Member label, relabeled by any <see cref="OctadLabelsAttribute"/> on the field. When renamed, the
		/// original compass name falls back into the tooltip so the octad's geometry stays discoverable.
		/// </summary>
		private GUIContent Label(SerializedProperty member, int index)
		{
			string name = member.displayName;
			string tooltip = member.tooltip;

			if (index < 8)
			{
				string custom = CustomLabel(index);
				if (!string.IsNullOrEmpty(custom))
				{
					if (string.IsNullOrEmpty(tooltip))
					{
						tooltip = $"{name} — {ElementColors.Name(index)}";
					}
					name = custom;
				}
				else if (string.IsNullOrEmpty(tooltip))
				{
					tooltip = ElementColors.Name(index);
				}
			}

			return new GUIContent(name, tooltip);
		}

		/// <summary>Relabel for <paramref name="index"/>, from literal names or the referenced octad asset.</summary>
		private string CustomLabel(int index)
		{
			if (!labelsResolved)
			{
				labelsAttribute = fieldInfo?.GetCustomAttribute<OctadLabelsAttribute>();
				labelsResolved = true;
			}

			if (labelsAttribute == null)
			{
				return null;
			}

			string[] labels = labelsAttribute.Labels ?? LabelsFromAsset(labelsAttribute.OctadResourcePath);
			return labels != null && index < labels.Length ? labels[index] : null;
		}

		/// <summary>
		/// Lane names pulled off a <see cref="StatOctadAsset"/>'s identifiers ("STATS/BODY/FIRE/Power" -> "Power"),
		/// so the inspector can't disagree with what the octad actually maps to. Cached; refreshes on recompile.
		/// </summary>
		private static string[] LabelsFromAsset(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			if (assetLabels.TryGetValue(path, out string[] cached))
			{
				return cached;
			}

			string[] labels = null;
			StatOctadAsset asset = Resources.Load<StatOctadAsset>(path);
			if (asset != null && asset.StatOctad != null)
			{
				StatOctad octad = asset.StatOctad;
				string[] identifiers =
				{
					octad.north, octad.northEast, octad.east, octad.southEast,
					octad.south, octad.southWest, octad.west, octad.northWest
				};

				labels = new string[8];
				for (int i = 0; i < 8; i++)
				{
					labels[i] = LastSegment(identifiers[i]);
				}
			}

			assetLabels[path] = labels;
			return labels;
		}

		private static string LastSegment(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
			{
				return null;
			}
			int slash = identifier.LastIndexOf('/');
			return slash >= 0 && slash < identifier.Length - 1 ? identifier[(slash + 1)..] : identifier;
		}

		private static IEnumerable<SerializedProperty> Members(SerializedProperty property)
		{
			SerializedProperty member = property.Copy();
			SerializedProperty end = property.GetEndProperty();
			bool enterChildren = true;
			while (member.NextVisible(enterChildren) && !SerializedProperty.EqualContents(member, end))
			{
				enterChildren = false;
				yield return member.Copy();
			}
		}
	}

	/// <summary>Plain number fields - text sits on the background, so it stays dark.</summary>
	[CustomPropertyDrawer(typeof(Vector8))]
	public class Vector8Drawer : OctadDrawerBase { }

	/// <summary>Plain number fields - text sits on the background, so it stays dark.</summary>
	[CustomPropertyDrawer(typeof(ElementOctad))]
	public class ElementOctadDrawer : OctadDrawerBase { }

	/// <summary>Dropdowns - text sits on the button, so it stays dark.</summary>
	[CustomPropertyDrawer(typeof(StatOctad))]
	public class StatOctadDrawer : OctadDrawerBase { }

	/// <summary>
	/// Replaces Unity's handle slider with a horizontal fill bar: for a distribution, relative weights read at a
	/// glance from the fill lengths instead of having to compare handle positions. Drag or type to edit.
	/// </summary>
	[CustomPropertyDrawer(typeof(RangedOctad))]
	public class RangedOctadDrawer : OctadDrawerBase
	{
		private const float FieldWidth = 48f;
		private const float Gap = 4f;
		private static readonly Color trackColor = new(0.16f, 0.16f, 0.16f);

		protected override void DrawMember(Rect rect, SerializedProperty member, GUIContent label, int index)
		{
			Rect body = EditorGUI.PrefixLabel(rect, label);
			Rect bar = new(body.x, body.y + 1f, Mathf.Max(0f, body.width - FieldWidth - Gap), body.height - 2f);
			Rect field = new(body.xMax - FieldWidth, body.y, FieldWidth, body.height);

			float value = Mathf.Clamp01(member.floatValue);

			// PrefixLabel already ate the indent, but EditorGUI controls re-apply it to any rect handed to them -
			// which would shove the field right (and shrink it) while the raw-drawn bar stays put.
			int indent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			try
			{
				EditorGUI.BeginChangeCheck();
				value = FillBar(bar, value, ElementColors.Get(index));
				value = EditorGUI.FloatField(field, value);
				if (EditorGUI.EndChangeCheck())
				{
					member.floatValue = Mathf.Clamp01(value);
				}
			}
			finally
			{
				EditorGUI.indentLevel = indent;
			}
		}

		/// <summary>Click/drag anywhere along the bar to set the value; the fill IS the readout.</summary>
		private static float FillBar(Rect rect, float value, Color color)
		{
			int id = GUIUtility.GetControlID(FocusType.Passive, rect);
			Event e = Event.current;

			switch (e.GetTypeForControl(id))
			{
				case EventType.MouseDown:
					if (e.button == 0 && rect.Contains(e.mousePosition))
					{
						GUIUtility.hotControl = id;
						GUIUtility.keyboardControl = 0;
						value = ValueAt(rect, e.mousePosition);
						GUI.changed = true;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == id)
					{
						value = ValueAt(rect, e.mousePosition);
						GUI.changed = true;
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == id)
					{
						GUIUtility.hotControl = 0;
						e.Use();
					}
					break;
				case EventType.Repaint:
					EditorGUI.DrawRect(rect, trackColor);
					if (value > 0f)
					{
						EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
					}
					break;
			}

			EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);
			return value;
		}

		private static float ValueAt(Rect rect, Vector2 mouse)
			=> rect.width <= 0f ? 0f : Mathf.Clamp01((mouse.x - rect.x) / rect.width);
	}

	/// <summary>Sliders - the track carries no text, so it can be overdriven into full color.</summary>
	[CustomPropertyDrawer(typeof(MinMaxOctad))]
	public class MinMaxOctadDrawer : OctadDrawerBase
	{
		protected override float BackgroundBoost => 1.6f;
	}
}
