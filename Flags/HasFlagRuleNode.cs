using UnityEngine;
using SpaxUtils.StateMachine;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRule"/> <see cref="RuleNodeBase"/> implementation that is valid when the configured flags are set.
	/// </summary>
	[NodeWidth(400)]
	public class HasFlagRuleNode : RuleNodeBase
	{
		public override bool Valid => valid;
		public override float Validity => flags.Length;

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.Rule inConnection;
		[SerializeField, ConstDropdown(typeof(IGameFlagConstants))] private string[] flags;
		[SerializeField] private bool invert;

		private FlagService flagsService;
		private bool valid;

		public override void OnPrepare()
		{
			base.OnPrepare();

			valid = false;
		}

		public void InjectDependencies(FlagService flagsService)
		{
			this.flagsService = flagsService;
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
			valid = flagsService.HasFlags(flags) != invert;
		}
	}
}
