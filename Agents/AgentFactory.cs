using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

namespace SpaxUtils
{
	public static class AgentFactory
	{
		public enum Callback
		{
			OnInject,
			OnActivate
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null)
		{
			return Create(setup, dependencyManager, Vector3.zero, Quaternion.identity,
				overrideName, labels, children, dependencies, progressCallback);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null)
		{
			return Create(setup, dependencyManager, position, Quaternion.identity,
				overrideName, labels, children, dependencies, progressCallback);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position, Quaternion rotation,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null)
		{
			return Create(setup.Identification, setup.Frame, setup.Body, dependencyManager, position, rotation,
				overrideName, labels,
				children == null ? setup.Children : setup.Children.Union(children),
				dependencies == null ? setup.Dependencies : setup.Dependencies.Union(dependencies),
				setup.Data,
				progressCallback);
		}

		public static Agent Create(
			IIdentification identification,
			Agent frame,
			AgentBodyComponent body,
			IDependencyManager dependencyManager,
			Vector3 position,
			Quaternion rotation,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			RuntimeDataCollection data = null,
			Action<Callback> progressCallback = null)
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

			// Set up identity.
			Agent agent = rootGo.GetComponent<Agent>();
			identification = new Identification(identification, agent);
			if (!string.IsNullOrEmpty(overrideName))
			{
				identification.Name = overrideName;
			}
			if (labels != null)
			{
				identification.Add(labels);
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
			if (data != null)
			{
				dependencyManager.Bind(data);
			}

			// Bind all dependency components.
			DependencyUtils.BindMonoBehaviours(rootGo, dependencyManager, includeChildren: true);

			// Inject all dependencies.
			progressCallback?.Invoke(Callback.OnInject);
			DependencyUtils.Inject(rootGo, dependencyManager, includeChildren: true, bindComponents: false);

			// Activate Agent and return.
			progressCallback?.Invoke(Callback.OnActivate);
			rootGo.SetActive(true);
			return agent;
		}
	}
}
