using System;
using System.Collections.Generic;
using System.Linq;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="StateMachine"/> wrapper that utilizes <see cref="BrainState"/>s for all the states, allowing dynamic restructuring of components and states.
	/// Transitions are done by calling <see cref="TryTransition(string, ITransition)"/>, if the target state does not exist it will be created automatically.
	/// </summary>
	public class Brain : IBrain
	{
		public event Action<IState> EnteredStateEvent;

		/// <inheritdoc/>
		public IState HeadState => stateMachine.HeadState;

		/// <inheritdoc/>
		public IReadOnlyList<IState> StateHierarchy => stateMachine.StateHierarchy;

		private Dictionary<string, BrainState> states;
		private StateMachine stateMachine;
		private Dictionary<StateMachineGraph, StateMachineGraph> graphInstances;

		public Brain(
			IDependencyManager dependencyManager,
			CallbackService callbackService,
			string defaultState,
			IEnumerable<BrainState> states = null,
			IEnumerable<StateMachineGraph> graphs = null)
		{
			DependencyManager brainDependencies = new DependencyManager(dependencyManager, "Brain");
			brainDependencies.Bind(this);
			stateMachine = new StateMachine(brainDependencies, callbackService);
			stateMachine.EnteredStateEvent += OnEnteredStateEvent;

			this.states = new Dictionary<string, BrainState>();
			if (states != null)
			{
				foreach (BrainState state in states)
				{
					AddState(state);
				}
			}

			graphInstances = new Dictionary<StateMachineGraph, StateMachineGraph>();
			if (graphs != null)
			{
				foreach (StateMachineGraph graph in graphs)
				{
					AppendGraph(graph);
				}
			}

			if (!string.IsNullOrEmpty(defaultState))
			{
				EnsureState(defaultState);
				stateMachine.SetDefaultState(defaultState);
			}
			else
			{
				SpaxDebug.Error($"Brain's default state is null or empty, this is not allowed.");
			}
		}

		public void Dispose()
		{
			stateMachine.Dispose();
			foreach (BrainState state in states.Values)
			{
				state.Dispose();
			}
			foreach (KeyValuePair<StateMachineGraph, StateMachineGraph> item in graphInstances)
			{
				// Destroy all graph instances.
				UnityEngine.Object.Destroy(item.Value);
			}
		}

		/// <inheritdoc/>
		public void Start()
		{
			stateMachine.TransitionToDefaultState();
		}

		#region States

		/// <inheritdoc/>
		public void AddState(BrainState state)
		{
			if (HasState(state.ID))
			{
				SpaxDebug.Error("Failed to add BrainState:", $"BrainState with ID \"{state.ID}\" already exists.");
				return;
			}

			states.Add(state.ID, state);
			stateMachine.AddState(state);
		}

		/// <inheritdoc/>
		public bool HasState(string id)
		{
			return states.ContainsKey(id);
		}

		/// <inheritdoc/>
		public bool TryGetState(string id, out BrainState state)
		{
			if (states.ContainsKey(id))
			{
				state = states[id];
				return true;
			}

			state = null;
			return false;
		}

		/// <inheritdoc/>
		public BrainState EnsureState(string id, IState template = null)
		{
			if (string.IsNullOrEmpty(id))
			{
				SpaxDebug.Error($"Tried to ensure state but the ID is null or empty!");
				return null;
			}

			// Retrieve parent state from template parent ID, if any.
			BrainState state = HasState(id) ? states[id] : null;

			// Ensure existence of state.
			if (state == null)
			{
				// State does not exist yet, create it using the template.
				BrainState parent = (template == null || template.Parent == null) ? null : EnsureState(template.Parent.ID, template.Parent);
				state = new BrainState(id, parent, (template == null || template.DefaultChild == null) ? null : template.DefaultChild.ID);
				states.Add(id, state);
				stateMachine.AddState(state, false);

				if (template != null)
				{
					foreach (IState child in template.Children.Values)
					{
						EnsureState(child.ID, child);
					}
				}
			}

			return state;
		}

		/// <inheritdoc/>
		public bool IsStateActive(string id)
		{
			return HeadState != null && StateHierarchy.Any((s) => s.ID == id);
		}

		#endregion States

		#region Transitions

		/// <inheritdoc/>
		public bool TryTransition(string id, ITransition transition = null)
		{
			EnsureState(id);
			return stateMachine.TryTransition(id, transition);
		}

		#endregion Transitions

		#region Components

		/// <inheritdoc/>
		public bool TryAddComponent(string id, IStateListener component)
		{
			return EnsureState(id).TryAddComponent(component);
		}

		/// <inheritdoc/>
		public bool TryRemoveComponent(string id, IStateListener component)
		{
			return EnsureState(id).TryRemoveComponent(component);
		}

		#endregion Components

		#region Graphs

		/// <inheritdoc/>
		public void AppendGraph(StateMachineGraph graph)
		{
			if (graphInstances.ContainsKey(graph))
			{
				SpaxDebug.Error("Graph is already appended to brain.", graph.name);
				return;
			}

			// Create instance of graph and add to appendices.
			StateMachineGraph instance = (StateMachineGraph)graph.Copy();
			graphInstances.Add(graph, instance);

			SpaxDebug.Notify($"[{GetHashCode()}] Graph instantiation success!", $"({graph.name})\n" +
				$"Instanced:\n\t-{string.Join("\n\t-", instance.nodes.Select((n) => $"[{n.GetHashCode()}] {n.GetType().FullName}"))}\n\n" +
				$"Prefab:\n\t-{string.Join("\n\t-", graph.nodes.Select((n) => $"[{n.GetHashCode()}] {n.GetType().FullName}"))}");

			// Go through all graph states and add their components to the corresponding BrainStates.
			List<IState> graphStates = instance.GetNodesOfType<IState>();
			foreach (IState graphState in graphStates)
			{
				BrainState brainState = EnsureState(graphState.ID, graphState);
				brainState.TryAddComponents(graphState.Components);
			}
		}

		/// <inheritdoc/>
		public void StripGraph(StateMachineGraph graph)
		{
			if (!graphInstances.ContainsKey(graph))
			{
				SpaxDebug.Error("Graph isn't present in brain.", graph.name);
				return;
			}

			// Go through all graph states and remove their components from the corresponding BrainStates.
			List<IState> graphStates = graphInstances[graph].GetNodesOfType<IState>();
			foreach (IState graphState in graphStates)
			{
				BrainState brainState = EnsureState(graphState.ID);
				brainState.TryRemoveComponents(graphState.Components);
			}

			// Destroy graph instance and remove from appendices.
			UnityEngine.Object.Destroy(graphInstances[graph]);
			graphInstances.Remove(graph);
		}

		#endregion Graphs

		private void OnEnteredStateEvent(IState state)
		{
			EnteredStateEvent?.Invoke(state);
		}
	}
}
