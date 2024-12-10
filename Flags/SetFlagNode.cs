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
		[SerializeField] private bool setID;
		[SerializeField] private bool complete;
		[SerializeField] private bool overwrite;

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

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			flagsService.SetFlags(flags, setID ? entity.ID : "", complete, overwrite);
		}
	}
}