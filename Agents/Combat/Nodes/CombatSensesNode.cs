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
			public IAgent Agent;
			public float LastSeen;
			public float Distance;

			public EnemyData(IAgent agent)
			{
				Agent = agent;
			}
		}

		private IAgent agent;
		private IVisionComponent vision;
		private IHittable hittable;
		private ICommunicationChannel comms;
		private ISpawnpoint spawnpoint;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;
		private ProjectileService projectileService;

		private Dictionary<ITargetable, EnemyData> enemies = new Dictionary<ITargetable, EnemyData>();

		public void InjectDependencies(IAgent agent, IVisionComponent vision,
			IHittable hittable, ICommunicationChannel comms, [Optional] ISpawnpoint spawnpoint,
			AgentStatHandler statHandler, CombatSensesSettings settings, ProjectileService projectileService)
		{
			this.agent = agent;
			this.vision = vision;
			this.hittable = hittable;
			this.comms = comms;
			this.spawnpoint = spawnpoint;
			this.statHandler = statHandler;
			this.settings = settings;
			this.projectileService = projectileService;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			agent.Mind.UpdatingEvent += OnMindUpdatingEvent;
			agent.Mind.MotivatedEvent += OnMindMotivatedEvent;
			hittable.Subscribe(this, OnReceivedHitEvent, -2);
			comms.Listen<HitData>(this, OnSentHitEvent);
			agent.Actor.PerformanceStartedEvent += OnPerformanceStartedEvent;
			agent.Targeter.Enemies.RemovedComponentEvent += OnEnemyTargetRemovedEvent;
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			agent.Mind.UpdatingEvent -= OnMindUpdatingEvent;
			agent.Mind.MotivatedEvent -= OnMindMotivatedEvent;
			hittable.Unsubscribe(this);
			comms.StopListening(this);
			agent.Actor.PerformanceStartedEvent -= OnPerformanceStartedEvent;
			agent.Targeter.Enemies.RemovedComponentEvent -= OnEnemyTargetRemovedEvent;
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

		private void OnMindUpdatingEvent(float delta)
		{
			// Invoked when the mind begins updating.
			GatherEnemyData();
			SendContinuousStimuli(delta);
		}

		private void GatherEnemyData()
		{
			// Get all enemies currently in view and store their relevant data.
			List<ITargetable> visible = vision.Spot(agent.Targeter.Enemies.Components);
			foreach (ITargetable inView in visible)
			{
				IAgent enemyAgent = inView.Entity as IAgent;
				if (enemyAgent.Dead)
				{
					continue;
				}

				EnemyData data;
				if (!enemies.ContainsKey(inView))
				{
					data = new EnemyData(enemyAgent);
					enemies.Add(inView, data);
					enemyAgent.DiedEvent += OnEnemyDiedEvent;
				}
				else
				{
					data = enemies[inView];
				}

				data.LastSeen = Time.time;
				data.Distance = Vector3.Distance(agent.Targetable.Center, inView.Center);
			}

			// Check for any enemies that have died or been out of view for too long and forget about them.
			List<ITargetable> outOfView = enemies.Keys.Except(visible).ToList();
			foreach (ITargetable lostTargetable in outOfView)
			{
				if (enemies[lostTargetable].Agent.Dead || Time.time - enemies[lostTargetable].LastSeen > settings.ForgetTime)
				{
					enemies[lostTargetable].Agent.DiedEvent -= OnEnemyDiedEvent;
					enemies.Remove(lostTargetable);
				}
			}
		}

		private void OnEnemyTargetRemovedEvent(ITargetable targetable)
		{
			if (enemies.ContainsKey(targetable))
			{
				enemies[targetable].Agent.DiedEvent -= OnEnemyDiedEvent;
				enemies.Remove(targetable);
			}
		}

		private void OnEnemyDiedEvent(IAgent enemy)
		{
			// Enemy has died, satisfy all motivations towards them.
			agent.Mind.Satisfy(Vector8.One * AEMOI.MAX_STIM, enemy);
		}

		private void OnReceivedHitEvent(HitData hitData)
		{
			// Invoked when the agent was hit by another entity.
			// Anger is proportionate to relative incoming force.
			// Fear is proportionate to relative incoming damage.
			// Hate is a combination of both.
			float anger = hitData.Result_Force / agent.Body.RigidbodyWrapper.Mass;
			float fear = hitData.Result_Damage / statHandler.PointStatOcton.SW.Current * 3f;
			Vector8 stim = new Vector8()
			{
				N = anger,
				S = fear,
				NW = anger + fear,
			};
			agent.Mind.Stimulate(stim, hitData.Hitter);
		}

		private void OnSentHitEvent(HitData hitData)
		{
			// Invoked when the agent has hit another entity (in combat).
			// Satisfies ???.

			//Vector8 satisfaction = new Vector8(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
			//agent.Mind.Satisfy(satisfaction, hitData.Hittable.Entity);
		}

		private void OnPerformanceStartedEvent(IPerformer performer)
		{
			if (performer is IMovePerformer movePerformer && movePerformer.Move is ICombatMove combatMove)
			{
				// Invoked when the agent performs an attack.
				// Satisfies Anger.
				Vector8 satisfaction = new Vector8() { N = movePerformer.Charge };
				agent.Mind.Satisfy(satisfaction, agent.Targeter.Target.Entity);
			}
		}

		private void SendContinuousStimuli(float delta)
		{
			// Projectile stimuli.
			// TODO: Split up projectile detection sense to a separte component which can be accesses by the behaviours.
			if (projectileService.IsTarget(agent.Targetable, out IProjectile[] projectiles))
			{
				foreach (IProjectile projectile in projectiles)
				{
					if (agent.Targetable.Center.Distance(projectile.StartPoint) < projectile.Range)
					{
						Vector8 current = agent.Mind.RetrieveStimuli(projectile.Source);

						Vector3 direction = agent.Targetable.Center - projectile.Point;
						float imminence = projectile.Speed / (direction.magnitude - (projectile.Radius + agent.Targetable.Size.x)).Max(1f); // The more imminent the projectile, the more dangerous.
						float danger = Mathf.Clamp(imminence * direction.ClampedDot(projectile.Velocity), 0f, AEMOI.MAX_STIM); // Dot: if projectile isn't pointing towards agent its not dangerous.
						Vector8 stim = new Vector8()
						{
							E = danger * current.E.InvertClamped().OutExpo(),
							W = danger * current.W.InvertClamped().OutExpo()
						};
						agent.Mind.Stimulate(stim * delta, projectile.Source);
					}
				}
			}

			// Tracked enemy stimuli.
			foreach (EnemyData enemy in enemies.Values)
			{
				Vector8 current = agent.Mind.RetrieveStimuli(enemy.Agent);

				float incitement = 0f;
				if (spawnpoint == null || spawnpoint.Region == null || spawnpoint.Region.IsInside(enemy.Agent.Transform.position))
				{
					// Only incite anger (action) if agent has no region or if enemy is within region.
					incitement = (enemy.Distance / (vision.Range * settings.InciteRange)).InvertClamped().Evaluate(settings.InciteCurve) / Mathf.Max(current.N * settings.StimDamping, 1f);
				}

				float threat = (enemy.Distance / (vision.Range * settings.ThreatRange)).InvertClamped().Evaluate(settings.ThreatCurve) / Mathf.Max(current.NW * settings.StimDamping, 1f);
				float danger = (statHandler.PointStatOcton.SW.PercentileMax.InvertClamped() * threat * 2f).Clamp01() / Mathf.Max(current.S * settings.StimDamping, 1f);
				float hostility = threat * -agent.Relations.Score(enemy.Agent.Identification).Min(0f);

				float carefulness = -AEMOI.MAX_STIM * danger.InvertClamped(); // Default is satisfaction if there is no danger.
				if (enemy.Agent.Actor.State == PerformanceState.Preparing &&
					enemy.Agent.Actor.MainPerformer is IMovePerformer movePerformer &&
					movePerformer.Move is ICombatMove combatMove)
				{
					carefulness = Mathf.InverseLerp(combatMove.Range * 1.5f, combatMove.Range, enemy.Distance) * AEMOI.MAX_STIM;// * (movePerformer.Charge * 2f).Min(2f);
				}
				float evade = carefulness / Mathf.Max(current.E * settings.StimDamping, 1f);
				float guard = carefulness / Mathf.Max(current.W * settings.StimDamping, 1f);

				Vector8 stim = new Vector8()
				{
					N = incitement, // Attacking
					E = evade, // Evasion
					S = danger, // Fleeing
					W = guard, // Guarding
					NW = hostility // Hating
				};

				//SpaxDebug.Log($"Stimulate ({enemy.Entity.Identification.Name}):", stim.ToStringShort());

				agent.Mind.Stimulate(stim * delta, enemy.Agent);
			}
		}
	}
}
