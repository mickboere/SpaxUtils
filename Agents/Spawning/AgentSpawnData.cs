using System.Collections.Generic;
using UnityEngine;
using System;

namespace SpaxUtils
{
	[Serializable]
	public class AgentSpawnData
	{
		[SerializeField, ConstDropdown(typeof(IIdentificationLabels))] private List<string> labels;
		[SerializeField] private ScriptableObject[] dependencies;
		[SerializeField] private LabeledDataCollection data;
		[SerializeField] private bool overwriteData;

		public Agent Spawn(IAgentSetup agentSetup, IDependencyManager dependencyManager, Vector3 position, Quaternion rotation)
		{
			Agent agent = AgentFactory.Create(agentSetup, dependencyManager, position, rotation, labels: labels, dependencies: dependencies);
			data.ApplyToEntity(agent, overwriteData);
			return agent;
		}
	}
}
