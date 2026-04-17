using SpaxUtils.StateMachines;
using UnityEngine;

namespace SpaxUtils
{
	public class BrainSuperimposerNode : StateComponentNodeBase
	{
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers), false, true)] private string superimposer;
		[SerializeField, ConstDropdown(typeof(IStateIdentifiers), true, true)] private string child;
		[SerializeField] private string layer;

		private IAgent agent;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Brain.SuperimposeState(superimposer, child, layer);
		}
	}
}
