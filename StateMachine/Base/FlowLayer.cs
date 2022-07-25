using System.Linq;
using System.Collections.Generic;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// <see cref="StateMachineLayer"/> implementation that checks for <see cref="ITransitionComponent"/>s to transition between states.
	/// </summary>
	public class FlowLayer : StateMachineLayer
	{
		private IState startState;
		private List<ITransitionComponent> transitions = new List<ITransitionComponent>();

		public FlowLayer(IState startState, IDependencyManager dependencyManager, CallbackService callbackService) : base(dependencyManager, callbackService)
		{
			this.startState = startState;
		}

		/// <summary>
		/// Will transition to the starting state of this flow.
		/// </summary>
		/// <param name="immediately">Should all transitions be skipped?</param>
		public void Start(bool immediately = false)
		{
			if (immediately)
			{
				EnterStateImmediately(startState);
			}
			else
			{
				EnterState(startState);
			}
		}

		protected override void EnteredState()
		{
			base.EnteredState();

			// For the transitions we only want the directly-connected components, so we get the direct outputs instead of searching in all the children.
			transitions = CurrentState.GetComponents().Where((child) => child is ITransitionComponent).Cast<ITransitionComponent>().ToList();
		}

		protected override void EndTransition()
		{
			base.EndTransition();

			// EndTransition indicates the current state is being stopped or a new one started, clear all transitions to stop them from being checked.
			transitions.Clear();
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();

			// Check transitions after components have been updated.
			CheckTransitions();
		}

		/// <summary>
		/// Finds a Valid transition with the highest Validity to determine the next state.
		/// </summary>
		private void CheckTransitions()
		{
			List<ITransitionComponent> validTransitions = transitions.Where((transition) => transition.Valid && transition.GetNextState() != null).ToList();

			if (validTransitions.Count > 0)
			{
				// Get the transition of the highest validity.
				ITransitionComponent targetTransition = validTransitions.First();
				foreach (ITransitionComponent transition in validTransitions)
				{
					if (transition.Validity > targetTransition.Validity)
					{
						targetTransition = transition;
					}
				}

				// We found our target transition, use it to enter the next state.
				EnterState(targetTransition.GetNextState());
			}
		}
	}
}
