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
		private const string TT_COMPONENTS = "Linked components will be activated while the condition is valid.\nThis allows you to run condition consequences without transitioning to another state node.";

		public override bool Valid => _valid;
		public override float Validity => _rules.Sum((r) => r.Validity);

		protected virtual bool RunComponents => true;

		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited), Tooltip(TT_COMPONENTS)] private Connections.StateComponent components;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] protected Connections.Rule rules;

		private CallbackService callbackService;

		private bool _valid;

		private List<IRule> _rules;
		private StateCallbackHelper ruleCallbacks;
		private List<IStateListener> _components;
		private StateCallbackHelper componentCallbacks;

		public void InjectDependencies(IDependencyManager dependencyManager, CallbackService callbackService)
		{
			this.callbackService = callbackService;

			_rules = GetOutputNodes<IRule>(nameof(rules));
			ruleCallbacks = new StateCallbackHelper(dependencyManager, _rules.Where(r => r.IsPureRule).ToList());
			_components = GetOutputNodes<IStateListener>(nameof(components));
			componentCallbacks = new StateCallbackHelper(dependencyManager, _components);

			ruleCallbacks.Inject();

			_valid = IsValid();

			if (Valid && RunComponents)
			{
				componentCallbacks.Inject();
			}
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState()
		{
			base.OnEnteringState();

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, 99998);

			ruleCallbacks.OnEnteringState();
			if (Valid && RunComponents)
			{
				componentCallbacks.OnEnteringState();
			}
		}

		/// <inheritdoc/>
		public override void WhileEnteringState(ITransition transition)
		{
			base.WhileEnteringState(transition);

			ruleCallbacks.WhileEnteringState(transition);
			if (Valid && RunComponents)
			{
				componentCallbacks.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateEntered()
		{
			base.OnStateEntered();

			ruleCallbacks.OnStateEntered();
			if (Valid && RunComponents)
			{
				componentCallbacks.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public override void OnExitingState()
		{
			base.OnExitingState();

			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);

			ruleCallbacks.OnExitingState();
			if (Valid && RunComponents)
			{
				componentCallbacks.OnExitingState();
			}
		}

		/// <inheritdoc/>
		public override void WhileExitingState(ITransition transition)
		{
			base.WhileExitingState(transition);

			ruleCallbacks.WhileExitingState(transition);
			if (Valid && RunComponents)
			{
				componentCallbacks.WhileExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateExit()
		{
			base.OnStateExit();

			ruleCallbacks.OnStateExit();
			if (Valid && RunComponents)
			{
				componentCallbacks.OnStateExit();
			}
		}

		#endregion

		private void OnUpdate(float delta)
		{
			bool valid = IsValid();
			if (Valid != valid || RunComponents != componentCallbacks.Running)
			{
				if (Valid && RunComponents)
				{
					// Became valid, activate subcomponents.
					componentCallbacks.QuickEnter();
				}
				else
				{
					// Became invalid, deactivate subcomponents.
					componentCallbacks.QuickExit();
				}
			}

			_valid = valid;
		}

		private bool IsValid()
		{
			if (_rules.Count > 0)
			{
				foreach (IRule rule in _rules)
				{
					if (!rule.Valid)
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
