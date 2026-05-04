using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Contains its own <see cref="Brain"/> initialised from <see cref="graph"/> and drives it
	/// by relaying the agent's brain state changes.
	/// <list type="bullet">
	/// <item><see cref="RelayMode.BestMatch"/> — walks head→root, transitions to the first recognised state.</item>
	/// <item><see cref="RelayMode.CopyHierarchy"/> — collects all recognised states root→head, reparents them
	/// to mirror the agent's filtered hierarchy, then transitions to the deepest match.</item>
	/// </list>
	/// If no match is found, transitions to <see cref="fallbackState"/>.
	/// </summary>
	public class RelayedBrain : AgentComponentBase
	{
		public enum RelayMode { BestMatch, CopyHierarchy }

		[SerializeField] private BrainGraph graph;
		[SerializeField] private RelayMode mode = RelayMode.BestMatch;
		[SerializeField, ConstDropdown(typeof(AgentStateIdentifiers))] private string fallbackState = AgentStateIdentifiers.INACTIVE;

		private IDependencyManager dependencyManager;
		private CallbackService callbackService;
		private IBrain localBrain;

		public void InjectDependencies(IDependencyManager dependencyManager, CallbackService callbackService)
		{
			this.dependencyManager = dependencyManager;
			this.callbackService = callbackService;
		}

		protected void OnEnable()
		{
			localBrain = new Brain(dependencyManager, callbackService, fallbackState, null, new[] { graph });
			localBrain.Start();
			Agent.Brain.EnteredStateEvent += OnAgentStateEntered;
		}

		protected void OnDisable()
		{
			Agent.Brain.EnteredStateEvent -= OnAgentStateEntered;
			localBrain.Dispose();
			localBrain = null;
		}

		private void OnAgentStateEntered(IState _)
		{
			IReadOnlyList<IState> hierarchy = Agent.Brain.StateHierarchy;

			if (mode == RelayMode.BestMatch)
			{
				for (int i = hierarchy.Count - 1; i >= 0; i--)
				{
					if (localBrain.HasState(hierarchy[i].ID))
					{
						localBrain.TryTransition(hierarchy[i].ID);
						return;
					}
				}
			}
			else
			{
				// Collect matching states in root→head order.
				var matches = new List<BrainState>();
				for (int i = 0; i < hierarchy.Count; i++)
				{
					if (localBrain.TryGetState(hierarchy[i].ID, out BrainState state))
					{
						matches.Add(state);
					}
				}

				if (matches.Count > 0)
				{
					// Reparent to mirror the agent's filtered hierarchy.
					matches[0].SetParent(null);
					for (int i = 1; i < matches.Count; i++)
					{
						matches[i].SetParent(matches[i - 1]);
					}

					localBrain.TryTransition(matches[^1].ID);
					return;
				}
			}

			if (!string.IsNullOrEmpty(fallbackState))
			{
				localBrain.TryTransition(fallbackState);
			}
		}
	}
}
