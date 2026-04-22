using System;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Specifies a background tint color for a node in the graph editor.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class NodeTintAttribute : Attribute
	{
		public Color color;

		/// <param name="hex">HTML hex color string, e.g. "#AB6F22"</param>
		public NodeTintAttribute(string hex)
		{
			ColorUtility.TryParseHtmlString(hex, out color);
		}

		/// <param name="r">Red 0-255</param>
		/// <param name="g">Green 0-255</param>
		/// <param name="b">Blue 0-255</param>
		public NodeTintAttribute(byte r, byte g, byte b)
		{
			color = new Color32(r, g, b, byte.MaxValue);
		}
	}
}
