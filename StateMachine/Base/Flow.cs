using System;
using System.Collections.Generic;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Static multi-layered state machine that requires a <see cref="StateMachineGraph"/> asset to run.
	/// Transitions are generally done through <see cref="IStateTransition"/> (nodes).
	/// </summary>
	public class Flow : IDisposable
	{
		public event Action StoppedFlowEvent;

		public bool Running => instance != null;

		private StateMachineGraph graph;
		private IDependencyManager dependencyManager;
		private IHistory history;

		private StateMachineGraph instance;
		private List<StateMachine> layers = new List<StateMachine>();

		public Flow(StateMachineGraph graph, IDependencyManager dependencyManager, IHistory history)
		{
			this.graph = graph;
			this.dependencyManager = new DependencyManager(dependencyManager, $"Flow_{graph.name}");
			this.dependencyManager.Bind(this);
			this.dependencyManager.Bind(history);
			this.history = history;
		}

		/// <summary>
		/// Creates a new instance of the <see cref="StateMachineGraph"/> and starts a new flow layer for every startnode.
		/// </summary>
		public void StartFlow()
		{
			if (Running)
			{
				SpaxDebug.Error("Flow is already running.");
				return;
			}

			instance = (StateMachineGraph)graph.Copy();

			// Collect starting states and create a flow layer for each.
			List<FlowStateNode> startStates = instance.GetStartStates();
			foreach (FlowStateNode startState in startStates)
			{
				StateMachine layer = new StateMachine(dependencyManager, dependencyManager.Get<CallbackService>(), instance.GetNodesOfType<IState>(), startState.ID);
				layers.Add(layer);
				history.Add(startState.ID);
				layer.EnteredStateEvent += OnEnteredStateEvent;
				layer.TransitionToDefaultState();
			}
		}

		/// <summary>
		/// Stops the currently running flow layers and disposes of the <see cref="StateMachineGraph"/> instance.
		/// </summary>
		public void StopFlow()
		{
			if (!Running)
			{
				SpaxDebug.Error("There is no running flow to stop.");
				return;
			}

			foreach (StateMachine layer in layers)
			{
				layer.EnteredStateEvent -= OnEnteredStateEvent;
				layer.Dispose();
			}
			layers.Clear();

			if (!instance)
			{
				SpaxDebug.Error("Attempting to stop flow but graph instance is null.");
			}
			else
			{
				UnityEngine.Object.Destroy(instance);
			}

			StoppedFlowEvent?.Invoke();
		}

		/// <summary>
		/// Stops the flow and disposes it.
		/// </summary>
		public void Dispose()
		{
			if (Running)
			{
				StopFlow();
			}
		}

		private void OnEnteredStateEvent(IState state)
		{
			history.Add(state.ID);
		}
	}
}
