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
	public class CombatSensesNode : StateComponentNodeBase
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

		private IAgent agent;
		private IVisionComponent vision;
		private IHittable hittable;
		private ICommunicationChannel comms;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;

		private Dictionary<ITargetable, EnemyData> enemies = new Dictionary<ITargetable, EnemyData>();

		public void InjectDependencies(IAgent agent, IVisionComponent vision,
			IHittable hittable, ICommunicationChannel comms,
			AgentStatHandler statHandler, CombatSensesSettings settings)
		{
			this.agent = agent;
			this.vision = vision;
			this.hittable = hittable;
			this.comms = comms;
			this.statHandler = statHandler;
			this.settings = settings;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			agent.Mind.UpdatingEvent += OnMindUpdatingEvent;
			agent.Mind.MotivatedEvent += OnMindMotivatedEvent;
			hittable.Subscribe(this, OnReceivedHitEvent, -2);
			comms.Listen<HitData>(this, OnSentHitEvent);
			agent.Actor.PerformanceUpdateEvent += OnPerformanceUpdateEvent;
			agent.Targeter.Enemies.RemovedComponentEvent += OnEnemyRemovedEvent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			agent.Mind.UpdatingEvent -= OnMindUpdatingEvent;
			agent.Mind.MotivatedEvent -= OnMindMotivatedEvent;
			hittable.Unsubscribe(this);
			comms.StopListening(this);
			agent.Actor.PerformanceUpdateEvent -= OnPerformanceUpdateEvent;
			agent.Targeter.Enemies.RemovedComponentEvent -= OnEnemyRemovedEvent;
		}

		private void OnMindUpdatingEvent(float delta)
		{
			// Invoked when the mind begins updating.
			GatherEnemyData();
			SendContinuousStimuli(delta);
		}

		private void OnMindMotivatedEvent()
		{
			// Invoked when the mind's motivation has settled.
			if (agent.Mind.Motivation.target != null)
			{
				// Set target to the entity responsible for the mind's motivation.
				agent.Targeter.SetTarget(agent.Mind.Motivation.target.GetEntityComponent<ITargetable>());
			}
			else
			{
				agent.Targeter.SetTarget(null);
			}
		}

		private void OnReceivedHitEvent(HitData hitData)
		{
			// Invoked when the agent was hit by another entity.
			// Process vertically to stimulate both Anger and Fear.
			// Anger is proportionate to relative incoming force.
			// Fear is proportionate to relative incoming damage.
			Vector8 stim = new Vector8(
				hitData.Result_Force / agent.Body.RigidbodyWrapper.Mass, 0f, 0f, 0f,
				hitData.Result_Damage / agent.GetStat(AgentStatIdentifiers.HEALTH) * 3f, 0f, 0f, 0f);
			agent.Mind.Stimulate(stim, hitData.Hitter);
		}

		private void OnSentHitEvent(HitData hitData)
		{
			// Invoked when the agent has hit another entity (in combat).
			// Satisfies Anger.
			Vector8 satisfaction = new Vector8(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
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
				//SpaxDebug.Log($"Satisfy Anger", $"{movePerformer.Charge}");
				// Invoked when the agent performs an attack.
				// Process satisfies Anger towards current target by the charge amount.
				Vector8 satisfaction = new Vector8(movePerformer.Charge, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
				agent.Mind.Satisfy(satisfaction, agent.Targeter.Target.Entity);
			}
		}

		private void GatherEnemyData()
		{
			// Get all enemies currently in view and store their relevant data.
			List<ITargetable> visible = vision.Spot(agent.Targeter.Enemies.Components);
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
				if (Time.time - enemies[lostTargetable].LastSeen > settings.ForgetTime)
				{
					enemies.Remove(lostTargetable);
				}
			}
		}

		private void SendContinuousStimuli(float delta)
		{
			foreach (EnemyData enemy in enemies.Values)
			{
				// STIM should be a combination of certain calculation about the enemy's status:
				// 
				// DANGER (how easy is it currently for the enemy to kill us) => instills fear, carefulness, desire for distance
				// (DIS)ADVANTAGE (difference in current stat values)
				//		Advantage	=> will prompt one to press this advantage (charge up for powerful attack).
				//		Disadvantage => will prompt one to better their odds (power up, seek better weapon, etc).
				// 

				Vector8 current = agent.Mind.Stimuli.ContainsKey(enemy.Entity) ? agent.Mind.Stimuli[enemy.Entity] : Vector8.Zero;

				float threat = (enemy.Distance / (vision.Range * settings.ThreatRange)).InvertClamped().Evaluate(settings.ThreatCurve) / Mathf.Max(current.NW * settings.StimDamping, 1f);
				float incite = (enemy.Distance / (vision.Range * settings.InciteRange)).InvertClamped().Evaluate(settings.InciteCurve) / Mathf.Max(current.N * settings.StimDamping, 1f);
				float danger = (statHandler.PointStatOcton.SW.PercentileMax.InvertClamped() * threat * 2f).Clamp01() / Mathf.Max(current.S * settings.StimDamping, 1f);
				float hate = threat * -agent.Relations.Score(enemy.Entity.Identification).Min(0f);

				Vector8 stim = new Vector8()
				{
					N = incite, // Anger
					S = danger, // Fear
					NW = hate // Hate
				};

				//SpaxDebug.Log($"Stimulate ({enemy.Entity.Identification.Name}):", stim.ToStringShort());

				agent.Mind.Stimulate(stim * delta, enemy.Entity);
			}
		}

		private void OnEnemyRemovedEvent(ITargetable targetable)
		{
			enemies.Remove(targetable);
		}
	}
}
