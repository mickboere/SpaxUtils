using UnityEngine;
using SpaxUtils.StateMachine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="BehaviourNodeBase"/> implementation that sets the configured flags upon entering the state.
	/// </summary>
	[NodeWidth(400)]
	public class SetFlagNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IGameFlagConstants))] private string[] flags;

		private FlagService flagsService;

		public void InjectDependencies(FlagService flagsService)
		{
			this.flagsService = flagsService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			flagsService.SetFlags(flags);
		}
	}
}