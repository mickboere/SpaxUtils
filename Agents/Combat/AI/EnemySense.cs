using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class EnemySense : IDisposable
	{
		public class EnemyData
		{
			public IAgent Agent;
			public float LastSeen;
			public Vector3 Direction;
			public float Distance;
			public float Hostility;
			public float Range;
			public float Threat;
			public float Advantage;
			public float Disadvantage;
			public float Oppurtunity;

			public EnemyData(IAgent agent)
			{
				Agent = agent;
			}
		}

		private Dictionary<ITargetable, EnemyData> enemies = new Dictionary<ITargetable, EnemyData>();

		private IAgent agent;
		private ISpawnpoint spawnpoint;
		private IVisionComponent vision;
		private AgentStatHandler statHandler;
		private CombatSensesSettings settings;

		public EnemySense(IAgent agent,
			 ISpawnpoint spawnpoint,
			 IVisionComponent vision,
			 AgentStatHandler statHandler,
			 CombatSensesSettings settings)
		{
			this.agent = agent;
			this.spawnpoint = spawnpoint;
			this.vision = vision;
			this.statHandler = statHandler;
			this.settings = settings;

			agent.Targeter.Enemies.RemovedComponentEvent += OnEnemyTargetRemovedEvent;
		}

		public void Dispose()
		{
			agent.Targeter.Enemies.RemovedComponentEvent -= OnEnemyTargetRemovedEvent;
		}

		public void Sense(float delta)
		{
			GatherEnemyData();
			SendContinuousStimuli(delta);
		}

		public EnemyData GetEnemyData()
		{
			if (agent.Targeter.Target == null || !enemies.ContainsKey(agent.Targeter.Target))
			{
				return null;
			}

			return enemies[agent.Targeter.Target];
		}

		public EnemyData GetEnemyData(ITargetable targetable)
		{
			return enemies[targetable];
		}

		#region Enemy Tracking

		private void GatherEnemyData()
		{
			float pointSum = statHandler.PointStatOcton.Vector8.Sum();

			// Get all enemies currently in view and store their relevant data.
			List<ITargetable> visible = vision.Spot(agent.Targeter.Enemies.Components);
			foreach (ITargetable inView in visible)
			{
				IAgent enemyAgent = inView.Entity as IAgent;
				if (enemyAgent.Dead)
				{
					continue;
				}

				EnemyData enemy;
				if (!enemies.ContainsKey(inView))
				{
					enemy = new EnemyData(enemyAgent);
					enemies.Add(inView, enemy);
					enemyAgent.DiedEvent += OnEnemyDiedEvent;
					enemy.Hostility = agent.Relations.Score(enemyAgent.Identification).Abs();
				}
				else
				{
					enemy = enemies[inView];
				}

				// - UPDATE ENEMY INTEL -
				enemy.LastSeen = Time.time;
				enemy.Direction = enemy.Agent.Transform.position - agent.Transform.position;
				enemy.Distance = enemy.Direction.magnitude;

				// Threat is defined by distance to enemy.
				enemy.Threat = (enemy.Distance / (vision.Range * settings.ThreatRange)).InvertClamped().Evaluate(settings.ThreatCurve);
				// Oppurtunity is defined by enemy being occupied.
				enemy.Oppurtunity = (enemy.Agent.Actor.State is PerformanceState.Performing ? 1f : 0.5f) * enemy.Threat.Invert();
				// (Dis)Advantage is defined by difference in current stat points.
				float enemyPointSum = enemy.Agent.GetEntityComponent<AgentStatHandler>().PointStatOcton.Vector8.Sum();
				enemy.Advantage = pointSum / enemyPointSum;
				enemy.Disadvantage = enemyPointSum / pointSum;
				enemy.Range = enemyAgent.GetStat(AgentStatIdentifiers.RANGE) +
					Mathf.Max(
						enemy.Agent.GetStat(AgentStatIdentifiers.RANGE.SubStat(AgentStatIdentifiers.SUB_LEFT_HAND)),
						enemy.Agent.GetStat(AgentStatIdentifiers.RANGE.SubStat(AgentStatIdentifiers.SUB_RIGHT_HAND)));
			}

			// Check for any enemies that have been out of view for too long and forget about them.
			List<ITargetable> outOfView = enemies.Keys.Except(visible).ToList();
			foreach (ITargetable lostTargetable in outOfView)
			{
				if (Time.time - enemies[lostTargetable].LastSeen > settings.ForgetTime)
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
			enemies[enemy.Targetable].Agent.DiedEvent -= OnEnemyDiedEvent;
			enemies.Remove(enemy.Targetable);
		}

		#endregion Enemy Tracking

		private void SendContinuousStimuli(float delta)
		{
			foreach (EnemyData enemy in enemies.Values)
			{
				Vector8 current = agent.Mind.RetrieveStimuli(enemy.Agent);

				// - GATHER INTEL -



				// - CALCULATE STIMULI -

				// Only incite anger if agent has no region or if enemy is within region.
				float incitement = 0f;
				if (spawnpoint == null || spawnpoint.Region == null || spawnpoint.Region.IsInside(enemy.Agent.Transform.position))
				{
					// Anger incitement is defined by current hate towards enemy.
					incitement = current.NW * (enemy.Distance / (vision.Range * settings.InciteRange)).InvertClamped().Evaluate(settings.InciteCurve);
				}

				// Desire to separate (fear) is defined by crucial stats that need time to recover.
				float separate = statHandler.PointStatOcton.SW.PercentileMax.Invert();
				separate += statHandler.PointStatOcton.W.PercentileRecoverable.Invert().Remap(-1f, 1f);
				separate *= separate > 0 ? enemy.Threat : 2f;

				// 
				float carefulness = -AEMOI.MAX_STIM * enemy.Threat.Invert(); // Default is satisfaction if there is no threat.
				if (enemy.Agent.Actor.State == PerformanceState.Preparing &&
					enemy.Agent.Actor.MainPerformer is IMovePerformer movePerformer &&
					movePerformer.Move is ICombatMove combatMove)
				{
					float range = combatMove.Range;
					if (combatMove is IMeleeCombatMove meleeCombatMove)
					{
						range += enemy.Agent.GetStat(AgentStatIdentifiers.RANGE.SubStat(meleeCombatMove.Limb)) ?? 0f;
					}

					carefulness = Mathf.InverseLerp(range + range, range, enemy.Distance).InOutSine() * AEMOI.MAX_STIM;
				}

				// - SEND STIMULI -

				Vector8 stim = new Vector8()
				{
					N = Damp(incitement, current.N), // Attacking
					NE = Damp(enemy.Oppurtunity, current.NE),
					E = carefulness, // Evading
									 //S = Damp(separate, current.S), // Fleeing
					S = separate, // Fleeing
					SW = Damp(Mathf.Max(enemy.Disadvantage - 1f, 0f), current.SW),
					W = carefulness, // Guarding
					NW = Damp(enemy.Threat * enemy.Hostility, current.NW) // Hating
				};

				//SpaxDebug.Log($"Stimulate ({enemy.Entity.Identification.Name}):", stim.ToStringShort());

				agent.Mind.Stimulate(stim * delta, enemy.Agent);
			}
		}

		private float Damp(float x, float y)
		{
			return x / Mathf.Max(y * settings.StimDamping, 1f);
		}
	}
}
