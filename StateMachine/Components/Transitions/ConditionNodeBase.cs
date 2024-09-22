using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="IStateTransition"/> that utilizes <see cref="IRule"/> nodes to determine whether the transition is valid.
	/// Requires all connected <see cref="RuleNodeBase"/> implementations to be valid in order to pass as valid itself.
	/// Can contain (sub) components that will be activated once the condition is valid.
	/// </summary>
	[NodeWidth(140)]
	public abstract class ConditionNodeBase : TransitionNodeBase
	{
		public override bool Valid => _valid;
		public override float Validity => _rules.Sum((r) => r.Validity);

		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] private Connections.StateComponent components;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] protected Connections.Rule rules;

		private IDependencyManager dependencyManager;
		private CallbackService callbackService;

		private bool _valid;
		private List<IStateComponent> _components;
		private List<RuleNodeBase> _rules;

		public void InjectDependencies(IDependencyManager dependencyManager, CallbackService callbackService)
		{
			this.dependencyManager = dependencyManager;
			this.callbackService = callbackService;

			_components = GetOutputNodes<IStateComponent>(nameof(components));
			_rules = GetOutputNodes<RuleNodeBase>(nameof(rules));

			foreach (RuleNodeBase rule in _rules)
			{
				dependencyManager.Inject(rule);
			}
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState()
		{
			base.OnEnteringState();

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, 99998);
			foreach (RuleNodeBase rule in _rules)
			{
				rule.OnEnteringState();
			}
		}

		/// <inheritdoc/>
		public override void WhileEnteringState(ITransition transition)
		{
			base.WhileEnteringState(transition);

			foreach (RuleNodeBase rule in _rules)
			{
				rule.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateEntered()
		{
			base.OnStateEntered();

			foreach (RuleNodeBase rule in _rules)
			{
				rule.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public override void OnExitingState()
		{
			base.OnExitingState();

			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);

			foreach (RuleNodeBase rule in _rules)
			{
				rule.OnExitingState();
			}

			if (Valid)
			{
				foreach (IStateComponent component in _components)
				{
					component.OnExitingState();
				}
			}
		}

		/// <inheritdoc/>
		public override void WhileExitingState(ITransition transition)
		{
			base.WhileExitingState(transition);

			foreach (RuleNodeBase rule in _rules)
			{
				rule.WhileExitingState(transition);
			}

			if (Valid)
			{
				foreach (IStateComponent component in _components)
				{
					component.WhileExitingState(transition);
				}
			}
		}

		/// <inheritdoc/>
		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (RuleNodeBase rule in _rules)
			{
				rule.OnStateExit();
			}

			if (Valid)
			{
				foreach (IStateComponent component in _components)
				{
					component.OnStateExit();
				}
			}
		}

		#endregion

		private void OnUpdate(float delta)
		{
			bool wasValid = _valid;
			_valid = IsValid();

			//SpaxDebug.Log("OnUpdate", $"valid:{_valid}/{wasValid}");

			if (_valid != wasValid)
			{
				if (_valid)
				{
					// Became valid, activate subcomponents.
					foreach (IStateComponent component in _components)
					{
						dependencyManager.Inject(component);
						component.OnEnteringState();
						component.OnStateEntered();
					}
				}
				else
				{
					// Became invalid, deactivate subcomponents.
					foreach (IStateComponent component in _components)
					{
						component.OnExitingState();
						component.OnStateExit();
					}
				}
			}
		}

		private bool IsValid()
		{
			foreach (IRule rule in _rules)
			{
				if (!rule.Valid)
				{
					return false;
				}
			}
			return true;
		}
	}
}
