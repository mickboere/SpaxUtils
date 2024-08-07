using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling all generic agent-related stats.
	/// </summary>
	public class AgentStatHandler : EntityComponentBase
	{
		[SerializeField] private List<PointsStat> pointsStats;

		private IAgent agent;

		private EntityStat recoveryStat;
		private FloatOperationModifier recoveryMod;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		protected void Awake()
		{
			// Initialize stat pairs.
			foreach (PointsStat pointStat in pointsStats)
			{
				pointStat.Initialize(agent);
			}

			// Modify recovery stat with control (so that recovery only occurs when agent is in control).
			if (agent.Body.HasRigidbody && agent.TryGetStat(AgentStatIdentifiers.RECOVERY, out recoveryStat))
			{
				recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				recoveryStat.AddModifier(this, recoveryMod);
			}
		}

		protected void OnDestroy()
		{
			// Clean up.
			if (recoveryStat != null)
			{
				recoveryStat.RemoveModifier(this);
			}
		}

		protected void Start()
		{
			// TODO: PLACEHOLDER! Recover must only be called upon respawning - not upon reloading.
			RecoverAll();
		}

		protected void Update()
		{
			if (recoveryMod != null)
			{
				recoveryMod.SetValue(agent.Body.RigidbodyWrapper.Control);
			}

			// Update state pairs to initiate recovery.
			// TODO: MUST BE DONE THROUGH BRAIN NODE TO PREVENT RECOVERY DURING DEATH.
			foreach (PointsStat pointStat in pointsStats)
			{
				pointStat.Update(Time.deltaTime * EntityTimeScale);
			}
		}

		/// <summary>
		/// Recovers all multistats.
		/// </summary>
		public void RecoverAll()
		{
			foreach (PointsStat pointStat in pointsStats)
			{
				pointStat.Recover();
			}
		}
	}
}
