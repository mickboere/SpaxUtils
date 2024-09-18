using System.Collections.Generic;
using UnityEngine;
using System;

namespace SpaxUtils
{
	[Serializable]
	public class AgentSpawnData
	{
		[SerializeField] private string overrideName;
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private List<string> labels;
		[SerializeField] private ScriptableObject[] dependencies;
		[SerializeField] private LabeledDataCollection data;
		[SerializeField] private bool overwriteData;

		public Agent Spawn(IAgentSetup agentSetup, IDependencyManager dependencyManager, Vector3 position, Quaternion rotation)
		{
			Agent agent = AgentFactory.Create(agentSetup, dependencyManager, position, rotation, overrideName: overrideName, labels: labels, dependencies: dependencies,
				progressCallback: (AgentFactory.Callback callback) =>
				{
					switch (callback)
					{
						case AgentFactory.Callback.OnInject:
							// Set data values before they're injected.
							RuntimeDataCollection runtimeData = dependencyManager.Get<RuntimeDataCollection>(createIfNull: false);
							if (runtimeData == null)
							{
								// Agent wasn't supplied with data, create a new collection and bind it.
								runtimeData = new RuntimeDataCollection(dependencyManager.Get<IIdentification>().ID);
								dependencyManager.Bind(runtimeData);
							}
							data.ApplyToRuntimeDataCollection(runtimeData, overwriteData);
							break;
					}
				});
			return agent;
		}
	}
}
