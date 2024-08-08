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

		[SerializeField, Range(0f, 1f)] private float hostileRange = 0.8f;
		[SerializeField, Range(0f, 1f)] private float engageRange = 0.4f;
		[SerializeField] private float forgetTime = 10f;

		private IAgent agent;
		private IEntityCollection entityCollection;
		private IVisionComponent vision;
		private IHittable hittable;
		private ICommunicationChannel comms;
		private EnemyIdentificationData enemyIdentificationData;

		private EntityComponentFilter<ITargetable> targetables;
		private Dictionary<ITargetable, EnemyData> enemies = new Dictionary<ITargetable, EnemyData>();

		public void InjectDependencies(IAgent agent, IEntityCollection entityCollection, IVisionComponent vision,
			IHittable hittable, ICommunicationChannel comms,
			[Optional] EnemyIdentificationData enemyIdentificationData)
		{
			this.agent = agent;
			this.entityCollection = entityCollection;
			this.vision = vision;
			this.hittable = hittable;
			this.comms = comms;
			this.enemyIdentificationData = enemyIdentificationData;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			agent.Mind.UpdateEvent += OnMindUpdateEvent;
			agent.Mind.MotivatedEvent += OnMindMotivatedEvent;
			hittable.Subscribe(this, OnReceivedHitEvent, -2);
			comms.Listen<HitData>(this, OnSentHitEvent);
			agent.Actor.PerformanceUpdateEvent += OnPerformanceUpdateEvent;

			List<string> hostiles = agent.GetDataValue<List<string>>(AIDataIdentifiers.HOSTILES);
			if (hostiles == null)
			{
				hostiles = new List<string>();
			}
			if (enemyIdentificationData != null)
			{
				hostiles.AddRange(enemyIdentificationData.EnemyLabels);
			}

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

			agent.Mind.UpdateEvent -= OnMindUpdateEvent;
			agent.Mind.MotivatedEvent -= OnMindMotivatedEvent;
			hittable.Unsubscribe(this);
			comms.StopListening(this);
			agent.Actor.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;

			targetables.Dispose();
		}

		private void OnMindUpdateEvent(float delta)
		{
			// Invoked when the mind begins updating.
			GatherEnemyData();
			SendContinuousStimuli(delta);
		}

		private void OnMindMotivatedEvent()
		{
			// Invoked when the mind's motivation has settled.

			// Set target to the entity responsible for the mind's motivation.
			if (agent.Targeter.Target != agent.Mind.Motivation.target)
			{
				if (agent.Mind.Motivation.target != null)
				{
					agent.Targeter.SetTarget(agent.Mind.Motivation.target.GetEntityComponent<ITargetable>());
				}
				else
				{
					agent.Targeter.SetTarget(null);
				}
			}
		}

		private void OnReceivedHitEvent(HitData hitData)
		{
			// Invoked when the agent was hit by another entity.
			// Process vertically to stimulate both Anger and Fear.
			// Anger is proportionate to relative incoming force.
			// Fear is proportionate to relative incoming damage.
			Vector8 stim = new Vector8(hitData.Result_Force / agent.Body.RigidbodyWrapper.Mass, 0f, 0f, 0f, hitData.Result_Damage / agent.GetStat(AgentStatIdentifiers.HEALTH), 0f, 0f, 0f);
			agent.Mind.Stimulate(stim, hitData.Hitter);
		}

		private void OnSentHitEvent(HitData hitData)
		{
			// Invoked when the agent has hit another entity (in combat).
			// Process vertically to satisfy both Anger and Fear.
			Vector8 satisfaction = new Vector8(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f);
			agent.Mind.Satisfy(satisfaction, hitData.Hittable.Entity);
		}

		private static readonly List<string> ATTACK_ACTS = new List<string>() { ActorActs.ATTACK_LIGHT, ActorActs.ATTACK_HEAVY }; // TODO: Temp solution.
		private void OnPerformanceUpdateEvent(IPerformer performer)
		{
			if (ATTACK_ACTS.Contains(performer.Act.Title) &&
				performer.State == PerformanceState.Performing &&
				performer.RunTime.Approx(0f) &&
				performer is IMovePerformer movePerformer)
			{
				SpaxDebug.Log($"Satisfy Anger", $"{movePerformer.Charge}");
				// Invoked when the agent performs an attack.
				// Process satisfies Anger towards current target by the charge amount.
				Vector8 satisfaction = new Vector8(movePerformer.Charge, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
				agent.Mind.Satisfy(satisfaction, agent.Targeter.Target.Entity);
			}
		}

		private void GatherEnemyData()
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

		private void SendContinuousStimuli(float delta)
		{
			foreach (EnemyData enemy in enemies.Values)
			{
				Vector8 stim = new Vector8();
				stim.NW = Mathf.InverseLerp(vision.Range, vision.Range * hostileRange, enemy.Distance) * 0.8f; // Hostility
				stim.N = Mathf.InverseLerp(vision.Range * hostileRange, vision.Range * engageRange, enemy.Distance); // Anger

				//SpaxDebug.Log($"Stimulate ({enemy.Entity.Identification.Name}):", stim.ToStringShort());

				agent.Mind.Stimulate(stim * delta, enemy.Entity);
			}
		}

		private void OnEntityRemovedEvent(ITargetable targetable)
		{
			enemies.Remove(targetable);
		}
	}
}
