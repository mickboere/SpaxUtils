using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component that blocks incoming damage if the agent is currently guarding.
	/// </summary>
	public class AgentGuardingComponent : EntityComponentBase
	{
		private IAgent agent;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

	}
}
