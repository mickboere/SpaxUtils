using SpaxUtils.StateMachines;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public static class AgentFactory
	{
		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager)
		{
			return Create(setup.Identification, setup.Frame, setup.Body, dependencyManager, Vector3.zero, Quaternion.identity, setup.BrainGraphs, setup.Children, setup.Dependencies);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position)
		{
			return Create(setup.Identification, setup.Frame, setup.Body, dependencyManager, position, Quaternion.identity, setup.BrainGraphs, setup.Children, setup.Dependencies);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position, Quaternion rotation)
		{
			return Create(setup.Identification, setup.Frame, setup.Body, dependencyManager, position, rotation, setup.BrainGraphs, setup.Children, setup.Dependencies);
		}

		public static Agent Create(
			IIdentification identification,
			Agent frame,
			AgentBodyComponent body,
			IDependencyManager dependencyManager,
			Vector3 position,
			Quaternion rotation,
			ICollection<StateMachineGraph> brainGraphs = null,
			ICollection<GameObject> children = null,
			ICollection<object> dependencies = null)
		{
			if (!dependencyManager.TryGetBinding(typeof(ICommunicationChannel), typeof(ICommunicationChannel), false, out _))
			{
				// Create and bind a new communication channel.
				dependencyManager.Bind(new CommunicationChannel($"AGENT_COMMS_{identification.ID}"));
			}

			// Instantiate the Agent Frame deactivated.
			GameObject rootGo = DependencyUtils.InstantiateDeactivated(frame.gameObject, position, rotation);

			// Instantiate the Agent's Body and other Children.
			GameObject bodyGo = DependencyUtils.InstantiateDeactivated(body.gameObject, rootGo.transform);
			bodyGo.SetActive(true);
			if (children != null)
			{
				foreach (GameObject child in children)
				{
					GameObject childInstance = DependencyUtils.InstantiateDeactivated(child, rootGo.transform);
					childInstance.SetActive(true);
				}
			}

			// Bind all dependencies.
			dependencyManager.Bind(identification);
			if (dependencies != null)
			{
				foreach (object dependency in dependencies)
				{
					dependencyManager.Bind(dependency);
				}
			}

			// Bind all dependency components.
			DependencyUtils.BindMonoBehaviours(rootGo, dependencyManager, includeChildren: true);

			// Inject all dependencies.
			DependencyUtils.Inject(rootGo, dependencyManager, includeChildren: true, bindComponents: false);

			// Initialize the brain.
			Agent agent = rootGo.GetComponent<Agent>();
			if (brainGraphs != null && brainGraphs.Count > 0)
			{
				agent.AddBrainGraphs(brainGraphs);
			}

			// Activate Agent and return.
			rootGo.SetActive(true);
			return agent;
		}
	}
}
