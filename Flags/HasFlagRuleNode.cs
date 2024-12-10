using UnityEngine;
using SpaxUtils.StateMachines;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRule"/> <see cref="RuleNodeBase"/> implementation that is valid when the configured flags are set.
	/// </summary>
	[NodeWidth(300)]
	public class HasFlagRuleNode : RuleNodeBase
	{
		public override bool Valid => _valid;
		public override float Validity => flags.Length;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField, ConstDropdown(typeof(IFlags))] private string[] flags;
		[SerializeField] private bool requireCompletion;
		[SerializeField] private bool invert;

		private FlagService flagsService;
		private bool _valid;

		public void InjectDependencies(FlagService flagsService)
		{
			this.flagsService = flagsService;
		}

		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);
			_valid = false;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			flagsService.SetFlagEvent += OnSetFlagEvent;
			UpdateValidity();
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			flagsService.SetFlagEvent -= OnSetFlagEvent;
		}

		private void OnSetFlagEvent(string flag, FlagData flagData)
		{
			if (flags.Contains(flag))
			{
				UpdateValidity();
			}
		}

		private void UpdateValidity()
		{
			_valid = requireCompletion ? flagsService.HasCompletedFlags(flags) != invert : flagsService.HasFlags(flags) != invert;
		}
	}
}
