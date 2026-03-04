using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Keeps an equipment item's derived Physics data in sync with its Rank, Quality, and PhysicsDistribution.
	/// Writes 8 physics values into the item's RuntimeData using identifiers from AgentStatHandler.Physics.
	/// </summary>
	public sealed class EquipmentInventoryBehaviour : IBehaviour
	{
		public bool Running { get; private set; }

		private RuntimeItemData runtimeItemData;
		private AgentStatHandler agentStatHandler;

		public void InjectDependencies(RuntimeItemData runtimeItemData, AgentStatHandler agentStatHandler)
		{
			this.runtimeItemData = runtimeItemData;
			this.agentStatHandler = agentStatHandler;
		}

		public void Start()
		{
			if (Running) return;
			Running = true;

			if (runtimeItemData == null || agentStatHandler == null) return;

			runtimeItemData.RuntimeData.DataUpdatedEvent += OnItemDataUpdated;
			RecalculatePhysics();
		}

		public void Stop()
		{
			if (!Running) return;
			Running = false;

			if (runtimeItemData != null && runtimeItemData.RuntimeData != null)
			{
				runtimeItemData.RuntimeData.DataUpdatedEvent -= OnItemDataUpdated;
			}
		}

		public void Dispose()
		{
			Stop();
		}

		private void OnItemDataUpdated(RuntimeDataEntry entry)
		{
			if (!Running || entry == null) return;

			if (entry.ID == ItemDataIdentifiers.RANK || entry.ID == ItemDataIdentifiers.QUALITY)
			{
				RecalculatePhysics();
			}
		}

		private void RecalculatePhysics()
		{
			if (!(runtimeItemData.ItemData is IEquipmentData eq)) return;

			StatOctad physicsIDs = agentStatHandler.Physics;
			if (physicsIDs == null) return;

			float budget = runtimeItemData.CalculateBudget();

			Vector8 lanePoints = SpaxFormulas.AllocatePointsForLevelRatios(eq.PhysicsDistribution, budget);

			for (int i = 0; i < 8; i++)
			{
				float lvl = lanePoints[i] <= 0f ? 0f : SpaxFormulas.LevelFromPoints(lanePoints[i]);
				float physic = Mathf.Round(lvl * 6f * eq.PhysicsScaling);

				string id = physicsIDs.GetIdentifier(i);
				if (!string.IsNullOrEmpty(id))
				{
					runtimeItemData.RuntimeData.SetValue(id, physic, createIfNull: true, dirty: false);
				}
			}
		}
	}
}
