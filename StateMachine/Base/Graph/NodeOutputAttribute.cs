using System;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Marks a serialized field as an output port on a <see cref="GraphNodeBase"/>.
	/// The field type determines the port's connection category.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class NodeOutputAttribute : Attribute
	{
		public ConnectionType connectionType;
		public TypeConstraint typeConstraint;

		public NodeOutputAttribute(
			ConnectionType connectionType = ConnectionType.Multiple,
			TypeConstraint typeConstraint = TypeConstraint.None)
		{
			this.connectionType = connectionType;
			this.typeConstraint = typeConstraint;
		}
	}
}
