using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Dynamic single layer multi-state machine that does not require a graph asset and does not support flow-transitions.
	/// Transitions are done by calling <see cref="EnterState(string)"/>, and does not require said state to already exist.
	/// <see cref="BrainState"/>s can have sub-states and parent states, upon entering a sub-state all of the components in its parent states will also be activated.
	/// </summary>
	public class Brain : IBrain
	{
		/// <inheritdoc/>
		public string CurrentState => stateMachine.CurrentState.Name;

		/// <inheritdoc/>
		public IReadOnlyList<string> StateHierarchy { get; private set; }

		private Dictionary<string, BrainState> states;
		private StateMachineLayer stateMachine;

		private StateMachineGraph graphInstance;

		public Brain(IDependencyManager dependencyManager, CallbackService callbackService, string startingState, StateMachineGraph graph = null)
		{
			states = new Dictionary<string, BrainState>();

			if (graph != null)
			{
				MirrorGraph(graph);
			}

			DependencyManager brainDependencies = new DependencyManager(dependencyManager, "Brain");
			brainDependencies.Bind(this);
			stateMachine = new StateMachineLayer(brainDependencies, callbackService);
			EnterState(startingState);
		}

		public void Dispose()
		{
			stateMachine.Dispose();
			foreach (BrainState state in states.Values)
			{
				state.Dispose();
			}
			if (graphInstance != null)
			{
				UnityEngine.Object.Destroy(graphInstance);
			}
		}

		/// <inheritdoc/>
		public void EnterState(string state)
		{
			// If the requested state does not exist it will be created as a Brain should be dynamic.
			BrainState brainState = EnsureState(state);

			// Construct state hierarchy.
			StateHierarchy = brainState.GetStateHierarchy();

			// Enter state.
			stateMachine.EnterState(brainState);
		}

		/// <inheritdoc/>
		public bool IsInState(string state)
		{
			return stateMachine.CurrentState != null && StateHierarchy.Contains(state);
		}

		#region Components

		/// <inheritdoc/>
		public void AddComponent(string state, IStateComponent component)
		{
			EnsureState(state).TryAddComponent(component);
			if (IsInState(state))
			{
				// Component was added to current state, refresh the state machine layer's components to activate it.
				stateMachine.RefreshComponents();
			}
		}

		/// <inheritdoc/>
		public void RemoveComponent(string state, IStateComponent component)
		{
			EnsureState(state).TryRemoveComponent(component);
			if (IsInState(state))
			{
				// Component was removed from current state, refresh the state machine layer's components to activate it.
				stateMachine.RefreshComponents();
			}
		}

		/// <inheritdoc/>
		public void MirrorGraph(StateMachineGraph graph)
		{
			if (graphInstance != null)
			{
				// Clear all existing mirrored graph data.
				List<BrainStateNode> nodes = graphInstance.nodes.Where((node) => node is BrainStateNode).Cast<BrainStateNode>().ToList();
				foreach (BrainStateNode brainStateNode in nodes)
				{
					List<IStateComponent> oldComponents = brainStateNode.GetAllComponents();
					BrainState state = EnsureState(brainStateNode.Name);
					foreach (IStateComponent c in oldComponents)
					{
						state.TryRemoveComponent(c);
					}
				}

				UnityEngine.Object.Destroy(graphInstance);
				graphInstance = null;
			}

			// Return if there is no graph to mirror.
			if (graph == null)
			{
				return;
			}

			// Create instance of graph and append data to brain.
			graphInstance = (StateMachineGraph)graph.Copy();
			List<BrainStateNode> brainStateNodes = graphInstance.nodes.Where((node) => node is BrainStateNode).Cast<BrainStateNode>().ToList();
			foreach (BrainStateNode brainStateNode in brainStateNodes)
			{
				BrainState state = EnsureState(brainStateNode.Name, brainStateNode);
				state.TryAddComponents(brainStateNode.GetAllComponents());
			}
			if (brainStateNodes.Any((n) => IsInState(n.Name)))
			{
				// Active state components have been modified, refresh the statemachine.
				stateMachine.RefreshComponents();
			}
		}

		#endregion

		#region States

		/// <summary>
		/// Returns whether the brain currently contains data for a state named <paramref name="state"/>.
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		private bool HasState(string state)
		{
			return states.ContainsKey(state);
		}

		/// <summary>
		/// Ensure a brainstate named <paramref name="state"/> exists.
		/// </summary>
		/// <param name="state">The state name to ensure.</param>
		/// <param name="copyHierarchy">An optional node hierarchy to copy over to this state.</param>
		/// <returns>A <see cref="BrainState"/> named <paramref name="state"/> and optionally <paramref name="copyHierarchy"/>'s hierarchy (but not components).</returns>
		private BrainState EnsureState(string state, IBrainState copyHierarchy = null)
		{
			if (!HasState(state))
			{
				// Add default empty state.
				states.Add(state, new BrainState(state));

				if (copyHierarchy != null)
				{
					// Copy node hierarchy.
					CopyHierarchy(state, copyHierarchy);
				}
			}

			return states[state];
		}

		/// <summary>
		/// Copies the hierarchy from <paramref name="copyHierarchy"/> over to the state named <paramref name="state"/>.
		/// </summary>
		/// <param name="state">The name of the state to copy the hierarchy over to.</param>
		/// <param name="copyHierarchy">The <see cref="BrainStateNode"/> of which to copy the hierarchy.</param>
		private void CopyHierarchy(string state, IBrainState copyHierarchy)
		{
			BrainState brainState = EnsureState(state);

			// Ensure parent state, if any.
			IBrainState parentNode = copyHierarchy.GetParentState();
			if (parentNode != null)
			{
				BrainState parentState = EnsureState(parentNode.Name, parentNode);
				brainState.TrySetParent(parentState, true);
			}

			// Ensure sub states, if any.
			List<IBrainState> subNodes = copyHierarchy.GetSubStates();
			if (subNodes != null && subNodes.Count > 0)
			{
				foreach (BrainStateNode sub in subNodes)
				{
					brainState.TryAddSubState(EnsureState(sub.Name, sub));
				}
			}
		}

		#endregion
	}
}
