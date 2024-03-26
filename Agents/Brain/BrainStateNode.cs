using UnityEngine;
using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="StateNodeBase"/> implementation for a brain state machine.
	/// </summary>
	[NodeTint("#239641"), NodeWidth(225)]
	public class BrainStateNode : StateNodeBase, IState
	{
		public override string ID => identifier;

		public override string UserFacingName => identifier;

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants)), FormerlySerializedAs("state")] private string identifier;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.State defaultChild;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.State children;
	}
}
