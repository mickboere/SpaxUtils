using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling an agent's grouped stat data.
	/// </summary>
	public class AgentStatHandler : EntityComponentBase
	{
		public PointStatOcton PointStatOcton => pointStatOcton;

		[SerializeField] private PointStatOcton pointStatOcton;

		private IAgent agent;

		private EntityStat recoveryStat;
		private FloatOperationModifier recoveryMod;

		public void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}

		protected void Awake()
		{
			// Initialize point stats.
			pointStatOcton.Initialize(agent);

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
			pointStatOcton.Update(Time.deltaTime);
		}

		/// <summary>
		/// Recovers all point stats.
		/// </summary>
		public void RecoverAll()
		{
			pointStatOcton.Recover();
		}
	}
}
