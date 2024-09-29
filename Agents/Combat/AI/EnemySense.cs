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
			public float Resentment;
			public float Reach;
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
			foreach (ITargetable enemy in agent.Targeter.Enemies.Components)
			{
				IAgent enemyAgent = enemy.Entity as IAgent;
				if (enemyAgent == null || !enemyAgent.Alive || (!enemies.ContainsKey(enemy) && !visible.Contains(enemy)))
				{
					// Enemy isn't an agent, is dead, or invisible and not being tracked; skip.
					continue;
				}

				EnemyData enemyData;
				if (!enemies.ContainsKey(enemy))
				{
					enemyData = new EnemyData(enemyAgent);
					enemies.Add(enemy, enemyData);
					enemyAgent.DiedEvent += OnEnemyDiedEvent;
					enemyData.Resentment = agent.Relations.Score(enemyAgent.Identification).Abs();
				}
				else
				{
					enemyData = enemies[enemy];
				}

				// - UPDATE ENEMY INTEL -
				enemyData.LastSeen = Time.time;
				enemyData.Direction = enemyData.Agent.Transform.position - agent.Transform.position;
				enemyData.Distance = enemyData.Direction.magnitude;

				// Threat is defined by distance to enemy.
				enemyData.Threat = (enemyData.Distance / (vision.Range * settings.ThreatRange)).InvertClamped().Evaluate(settings.ThreatCurve);
				// Oppurtunity is defined by enemy being occupied.
				enemyData.Oppurtunity = (enemyData.Agent.Actor.State is PerformanceState.Performing ? 1f : 0.5f) * enemyData.Threat.Invert();
				// (Dis)Advantage is defined by difference in current stat points.
				float enemyPointSum = enemyData.Agent.GetEntityComponent<AgentStatHandler>().PointStatOcton.Vector8.Sum();
				enemyData.Advantage = pointSum / enemyPointSum;
				enemyData.Disadvantage = enemyPointSum / pointSum;
				enemyData.Reach = enemyAgent.GetStat(AgentStatIdentifiers.REACH) +
					Mathf.Max(
						enemyData.Agent.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_LEFT_HAND)),
						enemyData.Agent.GetStat(AgentStatIdentifiers.REACH.SubStat(AgentStatIdentifiers.SUB_RIGHT_HAND)));
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

				// Only incite anger if agent has no region or if enemy is within region.
				float incitement = 0f;
				if (spawnpoint == null || spawnpoint.Region == null || spawnpoint.Region.IsInside(enemy.Agent.Transform.position))
				{
					// Anger incitement is defined by current hate towards enemy.
					incitement = current.NW * (enemy.Distance / (vision.Range * settings.InciteRange)).InvertClamped().Evaluate(settings.InciteCurve);
				}

				// Desire to retreat (fear) is defined by crucial stats that need time to recover.
				float retreat = statHandler.PointStatOcton.SW.PercentileMax.Invert(); // Health
				retreat += statHandler.PointStatOcton.W.PercentileRecoverable.Invert().Remap(-1f, 1f); // Endurance
				retreat *= retreat > 0 ? enemy.Threat : AEMOI.MAX_STIM; // If positive, scale stim by threat. If negative (stats are sufficiently recovered) maximally satisfy fear.

				// Danger is defined by how close to the attacking enemy's range one is.
				float danger = -AEMOI.MAX_STIM * enemy.Threat.Invert(); // Default is satisfaction if there is no threat.
				if (enemy.Agent.Actor.State == PerformanceState.Preparing &&
					enemy.Agent.Actor.MainPerformer is IMovePerformer movePerformer &&
					movePerformer.Move is ICombatMove combatMove)
				{
					float range = combatMove.Range;
					if (combatMove is IMeleeCombatMove meleeCombatMove)
					{
						range += enemy.Agent.GetStat(AgentStatIdentifiers.REACH.SubStat(meleeCombatMove.Limb)) ?? 0f;
					}

					danger = Mathf.InverseLerp(range + range, range, enemy.Distance).InOutSine() * AEMOI.MAX_STIM;
				}

				// - SEND STIMULI -

				Vector8 stim = new Vector8()
				{
					N = Damp(incitement, current, 0), // Attacking
					NE = Damp(enemy.Oppurtunity, current, 1), // Anticipating
					E = Damp(danger + retreat.Max(0f) * 0.5f, current, 2), // Evading
					S = Damp(retreat, current, 4), // Fleeing
					SW = Damp(Mathf.Max(enemy.Disadvantage - 1f, 0f), current, 5), // Powering
					W = Damp(danger, current, 6), // Guarding
					NW = Damp(enemy.Threat * enemy.Resentment, current, 7) // Hating
				};

				agent.Mind.Stimulate(stim * delta, enemy.Agent);
			}
		}

		private float Damp(float x, Vector8 c, int i, bool dampNegative = false)
		{
			return dampNegative || x > 0f ? x / Mathf.Max(c[i] * settings.StimDamping[i], 1f) : x;
		}
	}
}
