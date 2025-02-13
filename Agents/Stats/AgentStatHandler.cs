using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Component handling an agent's grouped stat data.
	/// </summary>
	public class AgentStatHandler : EntityComponentMono
	{
		public PointStatOctad PointStatOctad => pointStatOctad;

		[SerializeField, FormerlySerializedAs("pointStatOcton")] private PointStatOctad pointStatOctad;

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
			pointStatOctad.Initialize(agent);

			// Modify recovery stat with control (so that recovery only occurs when agent is in control).
			if (agent.Body.HasRigidbody && agent.TryGetStat(AgentStatIdentifiers.RECOVERY, out recoveryStat))
			{
				recoveryMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				recoveryStat.AddModifier(this, recoveryMod);
			}

			agent.RecoverEvent += RecoverAll;
		}

		protected void OnDestroy()
		{
			// Clean up.
			if (recoveryStat != null)
			{
				recoveryStat.RemoveModifier(this);
			}
			agent.RecoverEvent -= RecoverAll;
		}

		public void UpdateStats(float delta)
		{
			if (recoveryMod != null)
			{
				recoveryMod.SetValue(agent.Body.RigidbodyWrapper.Control);
			}

			pointStatOctad.Update(delta);
		}

		/// <summary>
		/// Recovers all point stats.
		/// </summary>
		public void RecoverAll()
		{
			pointStatOctad.Recover();
		}
	}
}
