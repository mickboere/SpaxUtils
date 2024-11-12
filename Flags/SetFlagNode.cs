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

		private IEntity entity;

		public void InjectDependencies(IEntity entity)
		{
			this.entity = entity;
		}

		public void InjectDependencies(FlagService flagsService)
		{
			this.flagsService = flagsService;
		}

		public override void OnEnteringState()
		{
			base.OnEnteringState();
			flagsService.SetFlags(entity.ID, flags);
		}
	}
}