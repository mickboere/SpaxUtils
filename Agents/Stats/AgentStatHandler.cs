using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling all drainable stats like health and stamina using <see cref="MultiStat"/>s.
	/// </summary>
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

		protected void Start()
		{
			// TODO: PLACEHOLDER! Recover must only be called upon respawning - not upon reloading.
			Recover();
		}

		protected void Update()
		{
			foreach (MultiStat pair in multiStats)
			{
				pair.Update(Time.deltaTime * EntityTimeScale);
			}
		}

		/// <summary>
		/// Recovers all multistats.
		/// </summary>
		public void Recover()
		{
			foreach (MultiStat pair in multiStats)
			{
				pair.Recover();
			}
		}
	}
}
