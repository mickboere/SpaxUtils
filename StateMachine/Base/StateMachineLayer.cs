using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// A single state machine layer, always has a single active <see cref="IState"/> of which the <see cref="IStateComponent"/>s are run.
	/// </summary>
	public class StateMachineLayer : IDisposable
	{
		public const float STUCK_WARNING_TIME = 7f;

		/// <summary>
		/// Event invoked when a new state is entered.
		/// </summary>
		public event Action<IState> EnteredStateEvent;

		/// <summary>
		/// The currently active state.
		/// </summary>
		public IState CurrentState { get; private set; }

		/// <summary>
		/// Returns true when this layer is currently transitioning between two states.
		/// </summary>
		public bool Transitioning => stateTransitionCoroutine != null;

		private IDependencyManager dependencyManager;
		private CallbackService callbackService;

		private List<IStateComponent> components = new List<IStateComponent>();
		private Coroutine stateTransitionCoroutine;

		public StateMachineLayer(IDependencyManager dependencyManager, CallbackService callbackService)
		{
			this.dependencyManager = new DependencyManager(dependencyManager, "StateMachineLayer");
			dependencyManager.Bind(this);
			this.callbackService = callbackService;

			callbackService.UpdateCallback += OnUpdate;
		}

		public void Dispose()
		{
			callbackService.UpdateCallback -= OnUpdate;
			EndTransition();
			ExitedState();
		}

		public void StopAndDispose(Action callback)
		{
			EndTransition();
			stateTransitionCoroutine = callbackService.StartCoroutine(StopEnumerator());

			IEnumerator StopEnumerator()
			{
				yield return ExitingState();
				Dispose();
			}
		}

		/// <summary>
		/// Will transition this flow layer to <paramref name="state"/>.
		/// </summary>
		/// <param name="state">The state to transition to.</param>
		public void EnterState(IState state)
		{
			EndTransition();
			stateTransitionCoroutine = callbackService.StartCoroutine(TransitionState(state));
		}

		/// <summary>
		/// Will immediately transition this flow layer to <paramref name="state"/>, skipping the <see cref="IStateComponent.OnEnteringState(Action)"/> and <see cref="IStateComponent.OnExitingState(Action)"/> callbacks.
		/// </summary>
		/// <param name="state">The state to immediately transition to.</param>
		public void EnterStateImmediately(IState state)
		{
			EndTransition();
			ExitedState();
			PrepareState(state);
			EnteredState();
		}

		/// <summary>
		/// Recollect the components from the <see cref="CurrentState"/> and initialize new additions while deactivating removed ones.
		/// </summary>
		public void RefreshComponents()
		{
			// First figure out which components have been newly added or removed.
			List<IStateComponent> oldComponents = new List<IStateComponent>(components);
			CollectComponentsFromCurrentState();
			List<IStateComponent> newComponents = components.Except(oldComponents).ToList();
			List<IStateComponent> removedComponents = oldComponents.Except(components).ToList();

			// Deactivate removed components
			removedComponents.ForEach((component) => component.OnStateExit());

			// Activate all the new components.
			newComponents.ForEach((component) => component.OnPrepare());
			newComponents.ForEach((component) => { if (component.InjectStateDependencies) { dependencyManager.Inject(component); } });
			newComponents.ForEach((component) => component.OnEnteringState(null)); // We're calling OnEnteringState because the component might rely on it, but we won't wait for its completion.
			newComponents.ForEach((component) => component.OnStateEntered());
		}

		private IEnumerator TransitionState(IState next)
		{
			yield return ExitingState();
			ExitedState();
			PrepareState(next);
			yield return EnteringState();
			EnteredState();
			stateTransitionCoroutine = null;
		}

		protected virtual void PrepareState(IState state)
		{
			SpaxDebug.Notify($"PrepareState: ", $"{state.Name}");

			CurrentState = state;
			CollectComponentsFromCurrentState();

			// Prepare
			CurrentState.OnPrepare();
			components.ForEach((component) => component.OnPrepare());

			// Inject
			if (CurrentState.InjectStateDependencies)
			{
				dependencyManager.Inject(CurrentState);
			}
			components.ForEach((component) => { if (component.InjectStateDependencies) { dependencyManager.Inject(component); } });
		}

		private IEnumerator EnteringState()
		{
			SpaxDebug.Log($"EnteringState: ", $"{CurrentState.Name}", LogType.Notify, callerOverride: "StateMachineLayer");

			// Will wait for entry transitions.
			int target = components.Count + 1;
			List<IStateComponent> completed = new List<IStateComponent>();
			CurrentState.OnEnteringState(() => completed.Add(CurrentState));
			components.ForEach((component) => component.OnEnteringState(() => completed.Add(component)));
			TimerStruct warningTimer = new TimerStruct(STUCK_WARNING_TIME);
			while (completed.Count < target)
			{
				if (!warningTimer.Paused && warningTimer.Expired)
				{
					SpaxDebug.Log($"Stuck EnteringState {CurrentState.Name}: ",
						$"Waited {warningTimer.Duration} seconds for:\n- {string.Join("\n- ", components.Union(new List<IStateComponent>() { CurrentState }).Except(completed).Select((c) => c.GetType().Name))}\n",
						LogType.Warning, callerOverride: "StateMachineLayer");
					warningTimer.Pause();
				}

				yield return null;
			}
		}

		protected virtual void EnteredState()
		{
			SpaxDebug.Notify($"EnteredState: ", $"{CurrentState.Name}");

			// State has been entered.
			CurrentState.OnStateEntered();
			components.ForEach((component) => component.OnStateEntered());
			EnteredStateEvent?.Invoke(CurrentState);
		}

		private IEnumerator ExitingState()
		{
			if (CurrentState == null)
			{
				yield break;
			}

			SpaxDebug.Log($"ExitingState: ", $"{CurrentState.Name}", LogType.Notify, callerOverride: "StateMachineLayer");

			// Will wait for exit transitions.
			int target = components.Count + 1;
			List<IStateComponent> completed = new List<IStateComponent>();
			CurrentState.OnExitingState(() => completed.Add(CurrentState));
			components.ForEach((component) => component.OnExitingState(() => completed.Add(component)));
			TimerStruct warningTimer = new TimerStruct(STUCK_WARNING_TIME);
			while (completed.Count < target)
			{
				if (!warningTimer.Paused && warningTimer.Expired)
				{
					SpaxDebug.Log($"Stuck ExitingState {CurrentState.Name}: ",
						$"Waited {warningTimer.Duration} seconds for:\n- {string.Join("\n- ", components.Union(new List<IStateComponent>() { CurrentState }).Except(completed).Select((c) => c.GetType().Name))}\n",
						LogType.Warning, callerOverride: "StateMachineLayer");
					warningTimer.Pause();
				}

				yield return null;
			}
		}

		protected virtual void ExitedState()
		{
			if (CurrentState == null)
			{
				return;
			}

			SpaxDebug.Notify($"ExitedState: ", $"{CurrentState.Name}");

			// State has been exited.
			CurrentState.OnStateExit();
			components.ForEach((component) => component.OnStateExit());
		}

		protected virtual void OnUpdate()
		{
			if (CurrentState == null || Transitioning)
			{
				return;
			}

			// Update state and components each frame.
			CurrentState.OnUpdate();
			components.ForEach((component) => component.OnUpdate());
		}

		protected virtual void EndTransition()
		{
			if (stateTransitionCoroutine != null)
			{
				callbackService.StopCoroutine(stateTransitionCoroutine);
				stateTransitionCoroutine = null;
			}
		}

		private void CollectComponentsFromCurrentState()
		{
			components = CurrentState.GetAllComponents();
			components.Reverse(); // Reverse the found children so that when looping over them we start with the deepest component layer.
			components = components.OrderBy((component) => component.ExecutionOrder).ToList(); // Finally order the nodes by priority as an extra layer on top of its depth.
		}
	}
}
