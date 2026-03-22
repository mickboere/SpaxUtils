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
		[SerializeField, ConstDropdown(typeof(IFlags), inputField: true)] private string[] flags;
		[SerializeField, Tooltip("Clears the flag data from the profile.")] private bool clear;
		[SerializeField, Conditional(nameof(clear), true), Tooltip("Mark the flag as being set by this Entity.")] private bool setID;
		[SerializeField, Conditional(nameof(clear), true), Tooltip("Mark the flag as being completed.")] private bool complete;
		[SerializeField, Conditional(nameof(clear), true), Tooltip("Overwrites any existing flag data.")] private bool overwrite;

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

			if (clear) flagsService.ClearFlags(flags);
			else flagsService.SetFlags(flags, setID ? entity.ID : "", complete, overwrite);
		}
	}
}