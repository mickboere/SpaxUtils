using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IRule"/> <see cref="RuleNodeBase"/> implementation that is valid when the configured flag requirements are met.
	/// </summary>
	[NodeWidth(300)]
	public class HasFlagRuleNode : RuleNodeBase
	{
		public override string UserFacingName
		{
			get
			{
				if (requirements == null || requirements.Length == 0)
				{
					return "Has Flag Rule";
				}

				if (requirements.Length > 1)
				{
					return $"Has Flags[{requirements.Length}] Rule";
				}

				FlagRequirement req = requirements[0];
				if (req.completed)
				{
					return $"Is flag \"{req.flag}\"{(req.invert ? " NOT " : " ")}Completed?";
				}

				return $"{(req.invert ? "Does NOT have" : "Has")} flag \"{req.flag}\"?";
			}
		}

		public override bool Valid => _valid;
		public override float Validity => requirements == null ? 0f : requirements.Length;

		[SerializeField, NodeInput] protected Connections.Rule inConnection;
		[SerializeField] private FlagRequirement[] requirements;

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
			if (requirements == null)
			{
				return;
			}

			for (int i = 0; i < requirements.Length; i++)
			{
				if (requirements[i].flag == flag)
				{
					UpdateValidity();
					return;
				}
			}
		}

		private void UpdateValidity()
		{
			_valid = FlagRequirement.EvaluateAll(requirements, flagsService);
		}
	}
}
