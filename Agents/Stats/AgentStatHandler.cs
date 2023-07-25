using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling all drainable stats like health and energy using <see cref="MultiStat"/>s.
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
			if (agent.Body.HasRigidbody && agent.TryGetStat(EntityStatIdentifier.MASS, out EntityStat mass))
			{
				mass.BaseValue = agent.Body.DefaultMass;
			}

			// Initialize stat pairs.
			foreach (MultiStat pair in multiStats)
			{
				pair.Initialize(agent);
			}
		}

		protected void Start()
		{
			// TODO: PLACEHOLDER! Recover must only be called upon respawning - not upon reloading.
			RecoverAll();
		}

		protected void Update()
		{
			// Update state pairs to initiate recovery.
			foreach (MultiStat pair in multiStats)
			{
				pair.Update(Time.deltaTime * EntityTimeScale);
			}
		}

		/// <summary>
		/// Recovers all multistats.
		/// </summary>
		public void RecoverAll()
		{
			foreach (MultiStat pair in multiStats)
			{
				pair.Recover();
			}
		}
	}
}
