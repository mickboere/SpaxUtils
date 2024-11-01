using UnityEngine;
using SpaxUtils.StateMachines;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Sets the configured flags upon entering the state.
	/// </summary>
	[NodeWidth(300)]
	public class SetFlagNode : StateComponentNodeBase
	{
		[SerializeField, ConstDropdown(typeof(IFlags))] private string[] flags;

		private FlagService flagsService;

		public void InjectDependencies(FlagService flagsService)
		{
			this.flagsService = flagsService;
		}

		public override void OnEnteringState()
		{
			base.OnEnteringState();
			flagsService.SetFlags(flags);
		}
	}
}