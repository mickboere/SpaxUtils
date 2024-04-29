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
		protected override string _defaultChild => hasDefaultChild ? defaultChild : null;

		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants)), FormerlySerializedAs("state")] private string identifier;
		[SerializeField, HideInInspector] private bool hasDefaultChild;
		[SerializeField, Conditional(nameof(hasDefaultChild), drawToggle: true), ConstDropdown(typeof(IStateIdentifierConstants))] private string defaultChild;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.State children;
	}
}
