using UnityEngine;

namespace SpaxUtils
{
	public class ConditionalAttribute : PropertyAttribute
	{
		/// <summary>
		/// The property to retrieve the bool value from.
		/// </summary>
		public string ToggleProperty { get; }

		/// <summary>
		/// The desired enum value, if <see cref="ToggleProperty"/> refers to an enum.
		/// </summary>
		public int EnumValue { get; }

		/// <summary>
		/// Should the condition be inversed, making true false and false true.
		/// </summary>
		public bool Inverse { get; }

		/// <summary>
		/// Should a copy of the toggle property's toggle be drawn to the right side of the field?
		/// </summary>
		public bool DrawToggle { get; }

		/// <summary>
		/// Should the field be hidden instead of disabled.
		/// </summary>
		public bool Hide { get; }

		public ConditionalAttribute(string toggleProperty, bool inverse = false, bool drawToggle = false, bool hide = true)
		{
			ToggleProperty = toggleProperty;
			EnumValue = -1;
			Inverse = inverse;
			DrawToggle = drawToggle;
			Hide = hide;
		}

		public ConditionalAttribute(string toggleProperty, int enumValue, bool inverse = false, bool drawToggle = false, bool hide = true)
		{
			ToggleProperty = toggleProperty;
			EnumValue = enumValue;
			Inverse = inverse;
			DrawToggle = drawToggle;
			Hide = hide;
		}
	}
}
