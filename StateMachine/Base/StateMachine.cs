using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// A single state machine layer, always has a single active <see cref="IState"/> of which the <see cref="IStateComponent"/>s are run.
	/// </summary>
	public class StateMachine : IDisposable
	{
		/// <summary>
		/// Event invoked when a new state is entered.
		/// </summary>
		public event Action<IState> EnteredStateEvent;

		/// <summary>
		/// All states present within this state machine.
		/// </summary>
		public IReadOnlyCollection<IState> States => states.Values;

		/// <summary>
		/// All states presently active.
		/// </summary>
		public IReadOnlyList<IState> StateHierarchy => hierarchy;

		/// <summary>
		/// The uppermost active state in the <see cref="StateHierarchy"/>.
		/// </summary>
		public IState HeadState => StateHierarchy.Count > 0 ? StateHierarchy[StateHierarchy.Count - 1] : null;

		/// <summary>
		/// The currently active transition, if any.
		/// </summary>
		public ITransition CurrentTransition { get; private set; }

		/// <summary>
		/// Whether this state machine is currently transitioning between two states.
		/// </summary>
		public bool Transitioning => CurrentTransition != null;

		private IDependencyManager dependencyManager;
		private CallbackService callbackService;

		private Dictionary<string, IState> states;
		private List<IState> hierarchy = new List<IState>();
		private List<IStateTransition> transitions = new List<IStateTransition>();
		private string defaultState;

		private Coroutine stateTransitionCoroutine;
		private List<IState> newHierarchy;
		private List<IState> exiting;
		private List<IState> entering;

		public StateMachine(IDependencyManager dependencyManager, CallbackService callbackService, List<IState> states = null, string defaultState = null)
		{
			this.dependencyManager = new DependencyManager(dependencyManager, "StateMachineLayer");
			this.dependencyManager.Bind(this);
			this.callbackService = callbackService;

			this.states = new Dictionary<string, IState>();
			if (states != null)
			{
				foreach (IState state in states)
				{
					this.states.Add(state.ID, state);
				}
			}

			callbackService.SubscribeUpdate(UpdateMode.Update, this, OnUpdate, 99999);

			this.defaultState = defaultState;
			if (defaultState != null)
			{
				TransitionToDefaultState();
			}
		}

		public void Dispose()
		{
			CancelTransition();
			callbackService.UnsubscribeUpdate(UpdateMode.Update, this);
		}

		private void OnUpdate()
		{
			CheckTransitions();
		}

		#region States

		public void AddState(IState state, bool reload = true)
		{
			if (!states.ContainsKey(state.ID))
			{
				states.Add(state.ID, state);
				if (reload)
				{
					Reload();
				}
				return;
			}

			SpaxDebug.Error("Error while adding state:", $"State with ID \"{state.ID}\" already exists.");
		}

		public void AddStates(IEnumerable<IState> states)
		{
			foreach (IState state in states)
			{
				AddState(state, false);
			}
			TransitionImmediately(HeadState.ID);
		}

		#endregion States

		#region Transitions

		/// <summary>
		/// Will re-transition to the current <see cref="HeadState"/>, <see cref="defaultState"/> if null.
		/// </summary>
		public void Reload()
		{
			if (HeadState != null)
			{
				TransitionImmediately(HeadState.ID);
			}
			else if (defaultState != null)
			{
				TransitionToDefaultState();
			}
		}

		/// <summary>
		/// Registers a new <see cref="IStateTransition"/>, allowing the StateMachine to automatically enter it if its conditions are met.
		/// </summary>
		public void AddStateTransition(IStateTransition stateTransition)
		{
			if (!transitions.Contains(stateTransition))
			{
				transitions.Add(stateTransition);
			}
		}

		/// <summary>
		/// Removes a registered <see cref="IStateTransition"/>, disabling the StateMachine from automatically entering it.
		/// </summary>
		public void RemoveStateTransition(IStateTransition stateTransition)
		{
			if (transitions.Contains(stateTransition))
			{
				transitions.Remove(stateTransition);
			}
		}

		/// <summary>
		/// Will immediately enter <paramref name="state"/> without a transition.
		/// </summary>
		/// <param name="state">The ID of the <see cref="IState"/> to transition to.</param>
		public void TransitionImmediately(string state)
		{
			if (!states.ContainsKey(state))
			{
				SpaxDebug.Error($"State with ID \"{state}\" could not be found.", $"Collection: [\"{string.Join("\", \"", states.Keys)}\"]");
				return;
			}

			CancelTransition();
			PrepareState(states[state]);
			EnterState(states[state]);
		}

		/// <summary>
		/// Will attempt to initiate a transition to <paramref name="state"/> using <paramref name="transition"/>.
		/// </summary>
		/// <param name="state">The ID of the <see cref="IState"/> to transition to.</param>
		/// <param name="transition">The intermediary transition object. When null and able an immediate transition will be initiated.</param>
		/// <returns>Whether the transition was successfully initiated.</returns>
		public bool TryTransition(string state, ITransition transition = null)
		{
			if (states.ContainsKey(state) && !Transitioning)
			{
				PrepareState(states[state]);

				if (transition == null || transition.Completed)
				{
					EnterState(states[state]);
				}
				else
				{
					stateTransitionCoroutine = callbackService.StartCoroutine(StartTransition(states[state], transition));
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Cancels current transition and re-enters the active <see cref="HeadState"/>.
		/// </summary>
		public void CancelTransition()
		{
			if (Transitioning)
			{
				DisposeTransition();
				PrepareState(HeadState);
				EnterState(HeadState);
			}
		}

		/// <summary>
		/// Changes the default (starting) state of the state machine.
		/// </summary>
		public void SetDefaultState(string defaultState)
		{
			this.defaultState = defaultState;
		}

		/// <summary>
		/// Will have the state machine transition back to the default state.
		/// </summary>
		public void TransitionToDefaultState(ITransition transition = null)
		{
			if (string.IsNullOrEmpty(defaultState))
			{
				SpaxDebug.Error("StateMachine does not have a default state defined.");
				return;
			}

			TryTransition(defaultState, transition);
		}

		private void DisposeTransition()
		{
			if (stateTransitionCoroutine != null)
			{
				callbackService.StopCoroutine(stateTransitionCoroutine);
				stateTransitionCoroutine = null;
			}

			CurrentTransition?.Dispose();
			CurrentTransition = null;

			newHierarchy = null;
			exiting = null;
			entering = null;
		}

		private void PrepareState(IState toState)
		{
			newHierarchy = toState.CollectActiveHierarchyRecursively();
			exiting = hierarchy.Except(newHierarchy).ToList();
			entering = newHierarchy.Except(hierarchy).ToList();

			foreach (IState state in exiting)
			{
				state.OnExitingState();
			}
			foreach (IState state in entering)
			{
				dependencyManager.Inject(state);
				state.OnEnteringState();
			}
		}

		private IEnumerator StartTransition(IState toState, ITransition transition)
		{
			CurrentTransition = transition;

			while (!transition.Completed)
			{
				foreach (IState state in exiting)
				{
					state.WhileExitingState(transition);
				}
				foreach (IState state in entering)
				{
					state.WhileEnteringState(transition);
				}
				yield return null;
			}

			EnterState(toState);
		}

		private void EnterState(IState toState)
		{
			hierarchy = newHierarchy;

			foreach (IState state in exiting)
			{
				state.OnStateExit();
			}
			foreach (IState state in entering)
			{
				state.OnStateEntered();
			}

			DisposeTransition();

			EnteredStateEvent?.Invoke(toState);
		}

		/// <summary>
		/// Finds a Valid transition with the highest Validity to determine the next state.
		/// </summary>
		private void CheckTransitions()
		{
			List<IStateTransition> validTransitions = transitions.Where((transition) => transition.Valid && transition.NextState != null).ToList();

			if (validTransitions.Count > 0)
			{
				// Get the transition of the highest validity.
				float validity = -1f;
				IStateTransition targetTransition = null;
				foreach (IStateTransition transition in validTransitions)
				{
					if (transition.Validity > validity)
					{
						validity = transition.Validity;
						targetTransition = transition;
					}
				}

				// We found our target transition, use it to enter the next state.
				TryTransition(targetTransition.NextState, targetTransition);
			}
		}

		#endregion Transitions
	}
}
