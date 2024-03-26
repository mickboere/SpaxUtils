using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="StateNodeBase"/> implementation for flow state machines.
	/// </summary>
	[NodeTint("#1A7223")]
	public class FlowStateNode : StateNodeBase
	{
		public override string UserFacingName =>
			(startState ? "[START] " : "") +
			(string.IsNullOrWhiteSpace(description) ? base.UserFacingName : description);

		public bool StartState => startState;

		[SerializeField, TextArea(1, 4)] private string description;
		[SerializeField] private bool startState;
	}
}
