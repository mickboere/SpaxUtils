using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Sets the configured flags upon entering the state.
	/// </summary>
	[NodeWidth(300)]
	public class SetFlagNode : StateComponentNodeBase
	{
		[SerializeField] private FlagSetting[] flags;
		[SerializeField, Tooltip("Mark the flags as being set by this Entity.")] private bool setID;

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
			FlagSetting.ApplyAll(flags, flagsService, setID ? entity.ID : "");
		}
	}
}
