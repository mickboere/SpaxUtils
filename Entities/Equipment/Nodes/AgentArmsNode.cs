using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Agent node that requests the arms be sheathed or unsheathed for the duration of this state.
	/// Deliberately does not write on exit — the incoming state's request is the only one that counts.
	/// </summary>
	public class AgentArmsNode : StateMachineNodeBase
	{
		[SerializeField, NodeInput] protected Connections.StateComponent inConnection;

		[SerializeField] private bool sheathe;

		private AgentArmsComponent arms;

		public void InjectDependencies(AgentArmsComponent arms)
		{
			this.arms = arms;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			arms.SetSheathed(sheathe);
		}
	}
}
