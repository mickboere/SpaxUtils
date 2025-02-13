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
			OnBindDependencies,
			OnInject,
			OnInjected
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null,
			bool activate = true)
		{
			return Create(setup, dependencyManager, Vector3.zero, Quaternion.identity,
				overrideName, labels, children, dependencies, progressCallback, activate);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null,
			bool activate = true)
		{
			return Create(setup, dependencyManager, position, Quaternion.identity,
				overrideName, labels, children, dependencies, progressCallback, activate);
		}

		public static Agent Create(IAgentSetup setup, IDependencyManager dependencyManager, Vector3 position, Quaternion rotation,
			string overrideName = null,
			IEnumerable<string> labels = null,
			IEnumerable<GameObject> children = null,
			IEnumerable<object> dependencies = null,
			Action<Callback> progressCallback = null,
			bool activate = true)
		{
			return Create(setup.Identification, setup.Frame, setup.Body, dependencyManager, position, rotation,
				overrideName, labels,
				children == null ? setup.Children : setup.Children.Union(children),
				dependencies == null ? setup.Dependencies : setup.Dependencies.Union(dependencies),
				setup.RetrieveDataClone(),
				progressCallback,
				activate);
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
			Action<Callback> progressCallback = null,
			bool activate = true)
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
			dependencyManager.Bind(identification);

			// Ensure agent has runtime data.
			RuntimeDataCollection injectorData = dependencyManager.Get<RuntimeDataCollection>(true, false);
			if (data == null)
			{
				if (injectorData == null)
				{
					// Agent wasn't supplied with any data, create a new collection and bind it.
					data = new RuntimeDataCollection(identification.ID);
				}
				else
				{
					// Use injected data.
					data = injectorData;
				}
			}
			else if (injectorData != null && data != injectorData)
			{
				// Combine data, but have injector data overwrite default data.
				data.Append(injectorData, true);
			}

			// Ensure the combined data is bound to dependency injector.
			dependencyManager.BindUnchecked(typeof(RuntimeDataCollection), data);

			// Ensure agent has unique seed.
			if (!data.TryGetValue(EntityDataIdentifiers.SEED, out int s))
			{
				// TODO: To truly keep agent seeds consistent across game-seeds, their ID's cannot be GUID's but need to be automatically indexed, like; "Name_X" where X is instance index of the agent type.
				int seed = dependencyManager.Get<RandomService>().GenerateSeed(identification.ID);
				data.SetValue(EntityDataIdentifiers.SEED, seed);
			}

			// Bind all dependencies.
			progressCallback?.Invoke(Callback.OnBindDependencies);
			if (dependencies != null)
			{
				foreach (object dependency in dependencies)
				{
					if (dependency is IDependencyProvider provider)
					{
						Dictionary<object, object> provided = provider.RetrieveDependencies();
						foreach (KeyValuePair<object, object> kvp in provided)
						{
							dependencyManager.Bind(kvp.Key, kvp.Value);
						}
					}
					else if (dependency is IDependencyFactory factory)
					{
						factory.Bind(dependencyManager);
					}
					else
					{
						dependencyManager.Bind(dependency);
					}
				}
			}

			// Bind all dependency components.
			DependencyUtils.BindMonoBehaviours(rootGo, dependencyManager, includeChildren: true);

			// Inject all dependencies.
			progressCallback?.Invoke(Callback.OnInject);
			DependencyUtils.Inject(rootGo, dependencyManager, includeChildren: true, bindComponents: false);
			progressCallback?.Invoke(Callback.OnInjected);

			// Complete.
			if (activate)
			{
				rootGo.SetActive(true);
			}

			return agent;
		}
	}
}
