using SpaxUtils.StateMachine;
using System.Collections.Generic;

namespace SpaxUtils
{
	public interface IBrainState : IState
	{
		/// <summary>
		/// Returns the connected parent-state node, if any.
		/// </summary>
		/// <returns>The connected parent-state node, if any.</returns>
		IBrainState GetParentState();

		/// <summary>
		/// Returns the connected sub-state nodes, if any.
		/// </summary>
		/// <returns>The connected sub-state nodes, if any.</returns>
		List<IBrainState> GetSubStates();

		/// <summary>
		/// Returns the parental state hierarchy including this state.
		/// </summary>
		/// <returns>The parental state hierarchy including this state.</returns>
		List<string> GetStateHierarchy();
	}
}
