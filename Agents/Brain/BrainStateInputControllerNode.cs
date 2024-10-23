using SpaxUtils.StateMachines;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaxUtils
{
	/// <summary>
	/// Class that listens for user-input in order to progress the brain state.
	/// </summary>
	[NodeTint("#eb34e8"), NodeWidth(225)]
	public class BrainStateInputControllerNode : StateMachineNodeBase
	{
		public override string UserFacingName => $"[{string.Join(",", actions)}] > {nextState}";

		[SerializeField, Input(backingValue = ShowBackingValue.Never)] private Connections.StateComponent inConnection;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string actionMap;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private List<string> actions;
		[SerializeField] private InputActionPhase phase;
		[SerializeField] private bool eatInput;
		[SerializeField] private int prio = 0;
		[SerializeField, ConstDropdown(typeof(IStateIdentifierConstants))] private string nextState;
		[SerializeField, Output(backingValue = ShowBackingValue.Never, typeConstraint = TypeConstraint.Inherited)] protected Connections.Rule rules;

		private Brain brain;
		private PlayerInputWrapper playerInputWrapper;
		private Dictionary<object, Option> options = new Dictionary<object, Option>();

		private List<IRule> _rules;
		private StateCallbackHelper ruleCallbacks;

		public void InjectDependencies(Brain brain, PlayerInputWrapper playerInputWrapper, IDependencyManager dependencyManager)
		{
			this.brain = brain;
			this.playerInputWrapper = playerInputWrapper;

			_rules = GetOutputNodes<IRule>(nameof(rules));
			ruleCallbacks = new StateCallbackHelper(dependencyManager, _rules.Where(r => r.IsPureRule).ToList());
			ruleCallbacks.Inject();
		}

		public override void OnEnteringState()
		{
			base.OnEnteringState();
			ruleCallbacks.OnEnteringState();
		}

		public override void WhileEnteringState(ITransition transition)
		{
			base.WhileEnteringState(transition);
			ruleCallbacks.WhileEnteringState(transition);
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			playerInputWrapper.RequestActionMaps(this, 0, actionMap);
			foreach (string action in actions)
			{
				options[action] = new Option(nextState, action, OnInputReceived, playerInputWrapper, eatInput, prio);
				options[action].MakeAvailable();
			}
			ruleCallbacks.OnStateEntered();
		}

		public override void OnExitingState()
		{
			base.OnExitingState();
			ruleCallbacks.OnExitingState();
		}

		public override void WhileExitingState(ITransition transition)
		{
			base.WhileExitingState(transition);
			ruleCallbacks.WhileExitingState(transition);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			foreach (KeyValuePair<object, Option> kvp in options)
			{
				kvp.Value.Dispose();
			}
			options.Clear();
			playerInputWrapper.CompleteActionMapRequest(this);
			ruleCallbacks.OnStateExit();
		}

		private void OnInputReceived(InputAction.CallbackContext context)
		{
			if (context.phase == phase && IsValid())
			{
				brain.TryTransition(nextState);
			}
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
