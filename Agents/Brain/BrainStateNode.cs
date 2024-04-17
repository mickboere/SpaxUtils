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
		protected override string _defaultChild => hasChildren ? defaultChild : null;

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants)), FormerlySerializedAs("state")] private string identifier;
		[SerializeField] private bool hasChildren;
		[SerializeField, Conditional(nameof(hasChildren), hide: true), ConstDropdown(typeof(IStateIdentifierConstants))] private string defaultChild;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.State children;
	}
}
