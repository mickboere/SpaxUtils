using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// AEMOI related node that stimulates the mind with combat senses.
	/// </summary>
	public class AgentCombatSenseNode : StateComponentNodeBase
	{
		private class EnemyData
		{
			public IEntity Entity;
			public float LastSeen;
			public float Distance;

			public EnemyData(IEntity entity)
			{
				Entity = entity;
			}
		}

		[SerializeField, Range(0f, 1f)] private float threatRange = 0.8f;
		[SerializeField, Range(0f, 1f)] private float engageRange = 0.6f;
		[SerializeField] private float forgetTime = 10f;

		private IAgent agent;
		private IEntityCollection entityCollection;
		private IVisionComponent vision;
		private EnemyIdentificationData enemyIdentificationData;

		private EntityComponentFilter<ITargetable> targetables;
		private Dictionary<ITargetable, EnemyData> enemies = new Dictionary<ITargetable, EnemyData>();

		public void InjectDependencies(IAgent agent, IEntityCollection entityCollection, IVisionComponent vision,
			[Optional] EnemyIdentificationData enemyIdentificationData)
		{
			this.agent = agent;
			this.entityCollection = entityCollection;
			this.vision = vision;
			this.enemyIdentificationData = enemyIdentificationData;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();
			agent.Mind.OnMindUpdateEvent += OnMindUpdateEvent;

			List<string> hostiles = agent.GetDataValue<List<string>>(AIDataIdentifiers.HOSTILES);
			if (hostiles == null)
			{
				hostiles = new List<string>();
			}
			if (enemyIdentificationData != null)
			{
				hostiles.AddRange(enemyIdentificationData.EnemyLabels);
			}

			SpaxDebug.Log($"{hostiles.Count} hostiles", $"enemyIdentificationData={(enemyIdentificationData == null ? "null" : $"present: ({enemyIdentificationData.EnemyLabels.Count})")}");

			targetables = new EntityComponentFilter<ITargetable>(
				entityCollection,
				(entity) => entity.Identification.HasAny(hostiles) || hostiles.Contains(entity.Identification.ID),
				(c) => true,
				agent);

			targetables.RemovedComponentEvent += OnEntityRemovedEvent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();
			agent.Mind.OnMindUpdateEvent -= OnMindUpdateEvent;

			targetables.Dispose();
		}

		private void OnMindUpdateEvent(float delta)
		{
			GatherEnemies();
			SendStimuli(delta);
		}

		private void GatherEnemies()
		{
			// Get all enemies currently in view and store their relevant data.
			List<ITargetable> visible = vision.Spot(targetables.Components);
			foreach (ITargetable inView in visible)
			{
				EnemyData data;
				if (!enemies.ContainsKey(inView))
				{
					data = new EnemyData(inView.Entity);
					enemies.Add(inView, data);
				}
				else
				{
					data = enemies[inView];
				}

				data.LastSeen = Time.time;
				data.Distance = Vector3.Distance(agent.Targetable.Center, inView.Center);
			}

			// Check for any enemies that have been out of view for too long and forget about them.
			List<ITargetable> outOfView = enemies.Keys.Except(visible).ToList();
			foreach (ITargetable lostTargetable in outOfView)
			{
				if (Time.time - enemies[lostTargetable].LastSeen > forgetTime)
				{
					enemies.Remove(lostTargetable);
				}
			}
		}

		private void SendStimuli(float delta)
		{
			foreach (EnemyData enemy in enemies.Values)
			{
				Vector8 stim = new Vector8();
				stim.NW = Mathf.InverseLerp(vision.Range, vision.Range * threatRange, enemy.Distance); // Hostility
				stim.N = Mathf.InverseLerp(vision.Range * threatRange, vision.Range * engageRange, enemy.Distance); // Anger

				SpaxDebug.Log($"Stimulate ({enemy.Entity.Identification.Name}):", stim.ToStringShort());

				agent.Mind.Stimulate(stim * delta, enemy.Entity);
			}
		}

		private void OnEntityRemovedEvent(ITargetable targetable)
		{
			enemies.Remove(targetable);
		}
	}
}
