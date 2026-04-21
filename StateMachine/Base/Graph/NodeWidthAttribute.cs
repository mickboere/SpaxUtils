using System;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Specifies the minimum display width for a node in the graph editor.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class NodeWidthAttribute : Attribute
	{
		public int width;

		public NodeWidthAttribute(int width)
		{
			this.width = width;
		}
	}
}
