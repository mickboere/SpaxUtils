using UnityEngine;
using SpaxUtils.StateMachine;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="StateNodeBase"/> implementation for a brain state machine.
	/// </summary>
	[NodeTint("#239641"), NodeWidth(225)]
	public class BrainStateNode : StateNodeBase, IBrainState
	{
		public override string Name => state;
		public override string UserFacingName => state;

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string state;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.State subStates;
		[SerializeField, Input(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited, connectionType = ConnectionType.Override)] private Connections.State parentState;

		/// <inheritdoc/>
		public IBrainState GetParentState()
		{
			return GetInputNode<IBrainState>(nameof(parentState));
		}

		/// <inheritdoc/>
		public List<IBrainState> GetSubStates()
		{
			return GetOutputNodes<IBrainState>(nameof(subStates));
		}
	}
}
