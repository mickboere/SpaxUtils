using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentStatHandler : EntityComponentBase
	{
		[SerializeField] private List<MultiStat> multiStats;

		private IAgent agent;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		protected void Awake()
		{
			foreach (MultiStat pair in multiStats)
			{
				pair.Initialize(agent);
			}
		}

		protected void Update()
		{
			foreach (MultiStat pair in multiStats)
			{
				pair.Update(Time.deltaTime);
			}
		}
	}
}
