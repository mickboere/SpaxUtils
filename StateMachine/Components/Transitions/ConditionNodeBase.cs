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
	public abstract class ConditionNodeBase : TransitionNodeBase
	{
		private const string TT_COMPONENTS = "Linked components will be activated while the condition is valid.\nThis allows you to run condition consequences without transitioning to another state node.";

		public override bool Valid => _valid;
		protected bool _valid;
		public override float Validity => _rules.Sum((r) => r.Validity);

		/// <summary>
		/// Whether linked components should be active.
		/// By default mirrors <see cref="_valid"/> so existing subclasses keep their behaviour.
		/// Subclasses can override to decouple component lifetime from rule validity.
		/// </summary>
		protected virtual bool RunComponents => _valid;

		[SerializeField, NodeOutput(typeConstraint: TypeConstraint.Inherited)] protected Connections.Rule rules;
		[SerializeField, NodeOutput(typeConstraint: TypeConstraint.Inherited), Tooltip(TT_COMPONENTS)] private Connections.StateComponent components;

		private CallbackService callbackService;

		private List<IRule> _rules;
		private StateCallbackHelper ruleCallbacks;
		private List<IStateListener> _components;
		private StateCallbackHelper componentCallbacks;

		public void InjectDependencies(IDependencyManager dependencyManager, CallbackService callbackService)
		{
			this.callbackService = callbackService;

			_rules = GetOutputNodes<IRule>(nameof(rules));
			ruleCallbacks = new StateCallbackHelper(dependencyManager, State, _rules.Where(r => r.IsPureRule).ToList());
			_components = GetOutputNodes<IStateListener>(nameof(components));
			componentCallbacks = new StateCallbackHelper(dependencyManager, State, _components);

			ruleCallbacks.Inject();

			_valid = IsValid();

			if (RunComponents)
			{
				componentCallbacks.Inject();
			}
		}

		#region Callbacks

		/// <inheritdoc/>
		public override void OnEnteringState(ITransition transition)
		{
			base.OnEnteringState(transition);

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, 99998);

			ruleCallbacks.OnEnteringState(transition);
			if (RunComponents)
			{
				componentCallbacks.OnEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public override void WhileEnteringState(ITransition transition)
		{
			base.WhileEnteringState(transition);

			ruleCallbacks.WhileEnteringState(transition);
			if (RunComponents)
			{
				componentCallbacks.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateEntered()
		{
			base.OnStateEntered();

			ruleCallbacks.OnStateEntered();
			if (RunComponents)
			{
				componentCallbacks.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public override void OnExitingState(ITransition transition)
		{
			base.OnExitingState(transition);

			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);

			ruleCallbacks.OnExitingState(transition);
			if (RunComponents)
			{
				componentCallbacks.OnExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public override void WhileExitingState(ITransition transition)
		{
			base.WhileExitingState(transition);

			ruleCallbacks.WhileExitingState(transition);
			if (RunComponents)
			{
				componentCallbacks.WhileExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public override void OnStateExit()
		{
			base.OnStateExit();

			ruleCallbacks.OnStateExit();
			if (RunComponents)
			{
				componentCallbacks.OnStateExit();
			}
		}

		#endregion

		private void OnUpdate(float delta)
		{
			bool valid = IsValid();
			if (_valid != valid || RunComponents != componentCallbacks.Active)
			{
				if (RunComponents)
				{
					// Components should be active, activate subcomponents.
					componentCallbacks.QuickEnter();
				}
				else
				{
					// Components should not be active, deactivate subcomponents.
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
