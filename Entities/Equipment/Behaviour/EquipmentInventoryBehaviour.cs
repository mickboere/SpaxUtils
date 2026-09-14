using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Keeps an equipment item's derived Physics data in sync with its Rank, Quality, and PhysicsDistribution.
	/// Writes 8 physics values into the item's RuntimeData using identifiers from AgentStatHandler.PhysicStats.
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
			if (Running)
			{
				return;
			}
			Running = true;

			if (runtimeItemData == null || agentStatHandler == null)
			{
				return;
			}

			runtimeItemData.RuntimeData.DataUpdatedEvent += OnItemDataUpdated;
			RecalculatePhysics();
		}

		public void Stop()
		{
			if (!Running)
			{
				return;
			}
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
			if (!Running || entry == null)
			{
				return;
			}

			if (entry.ID == ItemDataIdentifiers.RANK || entry.ID == ItemDataIdentifiers.QUALITY)
			{
				RecalculatePhysics();
			}
		}

		private void RecalculatePhysics()
		{
			if (!(runtimeItemData.ItemData is IEquipmentData eq))
			{
				return;
			}

			StatOctad physicsIDs = agentStatHandler.PhysicStats;
			if (physicsIDs == null)
			{
				return;
			}

			Vector8 physics = SpaxFormulas.EquipmentPhysics(eq.PhysicsDistribution, runtimeItemData.Rank, runtimeItemData.Quality, eq.PhysicsScaling);

			for (int i = 0; i < 8; i++)
			{
				float physic = physics[i];

				string id = physicsIDs.GetIdentifier(i);
				if (!string.IsNullOrEmpty(id))
				{
					runtimeItemData.RuntimeData.SetValue(id, physic, createIfNull: true, dirty: false);
				}
			}

			float effectiveMass = SpaxFormulas.EquipmentMass(eq.Mass, runtimeItemData.Rank, eq.PhysicsDistribution);
			runtimeItemData.RuntimeData.SetValue(ItemDataIdentifiers.MASS, effectiveMass, createIfNull: true, dirty: false);
		}
	}
}
