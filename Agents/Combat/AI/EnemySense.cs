using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class EnemySense : IDisposable
	{
		#region Constants
		// Proximity via time-to-hit.
		private const float SAFETY_PADDING = 0.25f;

		// Threat tuning constants.
		private const float MIN_APPROACH_SPEED = 0.1f;   // m/s, to avoid division by zero.
		private const float MAX_TIME_TO_HIT = 5f;        // seconds; beyond this, proximity threat ~ 0.
		private const float THREAT_SMOOTHING = 8f;       // higher = snappier.

		// Threat composition weights (should sum to 1).
		private const float THREAT_PROXIMITY_WEIGHT = 0.5f;
		private const float THREAT_LETHALITY_WEIGHT = 0.3f;
		private const float THREAT_INTENT_WEIGHT = 0.2f;

		// Lethality composition weights (should sum to 1).
		private const float LETHALITY_POINTS_WEIGHT = 0.5f;
		private const float LETHALITY_OFFENCE_WEIGHT = 0.3f;
		private const float LETHALITY_POWER_WEIGHT = 0.2f;
		#endregion Constants

		private readonly Dictionary<ITargetable, EnemyInfo> enemies = new Dictionary<ITargetable, EnemyInfo>();

		private readonly IAgent agent;
		private readonly IVisionComponent vision;
		private readonly AgentStatHandler statHandler;
		private readonly AgentCombatComponent combatComponent;
		private readonly CombatSensesSettings settings;

		private float pointSum;
		private List<ITargetable> visible;

		private HashSet<ITargetable> visibleSet = new HashSet<ITargetable>();
		private List<ITargetable> forgetBuffer = new List<ITargetable>(16);

		public EnemySense(
			IAgent agent,
			IVisionComponent vision,
			AgentStatHandler statHandler,
			AgentCombatComponent combatComponent,
			CombatSensesSettings settings)
		{
			this.agent = agent;
			this.vision = vision;
			this.statHandler = statHandler;
			this.combatComponent = combatComponent;
			this.settings = settings;

			agent.Targeter.Enemies.RemovedComponentEvent += OnEnemyTargetRemovedEvent;
		}

		public void Dispose()
		{
			agent.Targeter.Enemies.RemovedComponentEvent -= OnEnemyTargetRemovedEvent;
		}

		public void Sense(float delta)
		{
			GatherEnemyData(delta);
			SendContinuousStimuli(delta);
		}

		/// <summary>
		/// Retrieve the <see cref="EnemyInfo"/> for the currently targeted entity.
		/// </summary>
		public EnemyInfo GetEnemyInfo()
		{
			if (agent.Targeter.Target == null || !enemies.ContainsKey(agent.Targeter.Target))
			{
				return null;
			}

			return enemies[agent.Targeter.Target];
		}

		public EnemyInfo GetEnemyInfo(ITargetable targetable)
		{
			enemies.TryGetValue(targetable, out EnemyInfo info);
			return info;
		}

		#region Enemy Tracking

		private void GatherEnemyData(float delta)
		{
			pointSum = statHandler.PointStats.Vector8.Sum();

			List<ITargetable> enemyList = agent.Targeter.Enemies.Components;

			visible = vision.Spot(enemyList);

			visibleSet.Clear();
			for (int i = 0; i < visible.Count; i++)
			{
				visibleSet.Add(visible[i]);
			}

			for (int i = 0; i < enemyList.Count; i++)
			{
				ITargetable enemy = enemyList[i];

				// Unity fake-null guard for interface references.
				if (enemy is MonoBehaviour mb && !mb)
				{
					continue;
				}

				IAgent enemyAgent = enemy.Entity as IAgent;

				if (enemyAgent == null || !enemyAgent.Alive || (!enemies.ContainsKey(enemy) && !visibleSet.Contains(enemy)))
				{
					// Enemy is not an agent, is dead, or invisible and not being tracked; skip.
					continue;
				}

				EnemyInfo enemyData;

				if (!enemies.ContainsKey(enemy))
				{
					enemyData = new EnemyInfo(enemyAgent);
					enemies.Add(enemy, enemyData);
					enemyAgent.DiedEvent += OnEnemyDiedEvent;
					enemyData.Resentment = -agent.Relations.Score(enemyAgent.Identification);
				}
				else
				{
					enemyData = enemies[enemy];
				}

				if (enemyData.Agent == agent)
				{
					SpaxDebug.Error($"[{agent.Identification.TagFull()}] Target is self, this should not be possible.");
					continue;
				}

				UpdateEnemyInfo(enemy, enemyData, delta);
			}

			// Forget enemies out of view for too long (no LINQ allocations).
			forgetBuffer.Clear();

			foreach (KeyValuePair<ITargetable, EnemyInfo> kv in enemies)
			{
				ITargetable t = kv.Key;

				if (t is MonoBehaviour mb && !mb)
				{
					forgetBuffer.Add(t);
					continue;
				}

				if (!visibleSet.Contains(t))
				{
					if (Time.time - kv.Value.LastSeen > settings.ForgetTime)
					{
						forgetBuffer.Add(t);
					}
				}
			}

			for (int i = 0; i < forgetBuffer.Count; i++)
			{
				ITargetable lostTargetable = forgetBuffer[i];

				if (lostTargetable != null && enemies.TryGetValue(lostTargetable, out EnemyInfo info))
				{
					info.Agent.DiedEvent -= OnEnemyDiedEvent;
					enemies.Remove(lostTargetable);
				}
				else
				{
					enemies.Remove(lostTargetable);
				}
			}
		}

		private void UpdateEnemyInfo(ITargetable enemy, EnemyInfo info, float delta)
		{
			// Visibility.
			if (visibleSet.Contains(enemy))
			{
				info.Visible = true;
				info.LastSeen = Time.time;
				info.LastLocation = info.Agent.Transform.position;

				// Spatial.
				Vector3 toEnemy = info.Agent.Transform.position - agent.Transform.position;
				info.Distance = toEnemy.magnitude;
				info.Direction = info.Distance > Mathf.Epsilon ? toEnemy / info.Distance : Vector3.zero;

				// Relative velocity (enemy - self). ClosingSpeed > 0 when they are closing in.
				Vector3 relVel = info.Agent.Body.RigidbodyWrapper.Velocity - agent.Body.RigidbodyWrapper.Velocity;
				info.ClosingSpeed = -Vector3.Dot(relVel, info.Direction);
			}
			else
			{
				info.Visible = false;
			}

			// Lethality of enemy to agent.
			float enemyPointSum = info.StatHandler.PointStats.Vector8.Sum();
			float pointRatio = enemyPointSum <= Mathf.Epsilon ? 0.5f : enemyPointSum / pointSum;
			float offenseRatio = info.CombatComp.Offense / Mathf.Max(combatComponent.Poise, 0.001f);
			float powerRatio = info.CombatComp.Power / agent.Body.RigidbodyWrapper.Mass;

			float pointLeth = pointRatio / (pointRatio + 1f);
			float offenseLeth = offenseRatio / (offenseRatio + 1f);
			float powerLeth = powerRatio / (powerRatio + 1f);

			info.Lethality = Mathf.Clamp01(
				LETHALITY_POINTS_WEIGHT * pointLeth +
				LETHALITY_OFFENCE_WEIGHT * offenseLeth +
				LETHALITY_POWER_WEIGHT * powerLeth);

			float effectiveReach = 0.5f;
			if (info.CombatComp != null)
			{
				effectiveReach = info.CombatComp.ActiveReach + SAFETY_PADDING;
			}

			float distanceToCover = Mathf.Max(info.Distance - effectiveReach, 0f);

			if (distanceToCover <= 0f)
			{
				info.TimeToHit = 0f;
			}
			else if (info.ClosingSpeed <= 0f)
			{
				info.TimeToHit = float.PositiveInfinity;
			}
			else
			{
				float approachSpeed = Mathf.Max(info.ClosingSpeed, MIN_APPROACH_SPEED);
				info.TimeToHit = distanceToCover / approachSpeed;
			}

			float proximityThreat;
			if (info.Distance <= effectiveReach)
			{
				proximityThreat = 1f;
			}
			else if (float.IsPositiveInfinity(info.TimeToHit))
			{
				proximityThreat = 0f;
			}
			else
			{
				float tNorm = Mathf.Clamp01(info.TimeToHit / MAX_TIME_TO_HIT);
				proximityThreat = 1f - Mathf.SmoothStep(0f, 1f, tNorm);
			}

			// Intent (combat state + facing).
			float facingToSelf = 0f;
			if (info.Distance > Mathf.Epsilon)
			{
				Vector3 toSelf = (agent.Transform.position - info.Agent.Transform.position).normalized;
				facingToSelf = Mathf.Clamp01(Vector3.Dot(info.Agent.Transform.forward, toSelf));
			}

			float rawIntent = 0f;
			if (info.CombatComp != null)
			{
				if (info.CombatComp.InCombatMode)
				{
					rawIntent += 0.5f;
				}

				if (info.CombatComp.CurrentCombatMove != null)
				{
					rawIntent += 0.5f;
				}
			}

			rawIntent = Mathf.Clamp01(rawIntent);
			info.Intent = rawIntent * facingToSelf;

			// Final Threat in [0,1].
			float threat =
				THREAT_PROXIMITY_WEIGHT * proximityThreat +
				THREAT_LETHALITY_WEIGHT * info.Lethality +
				THREAT_INTENT_WEIGHT * info.Intent;

			threat = Mathf.Clamp01(threat);

			float lerpFactor = 1f - Mathf.Exp(-THREAT_SMOOTHING * delta);
			info.Threat = Mathf.Lerp(info.Threat, threat, lerpFactor);

			// Opportunity (enemy open to offence).
			float openness = info.CombatComp != null ? info.CombatComp.Openness : 0f;
			info.Oppurtunity = info.Direction.NormalizedDot(info.Agent.Transform.forward) * openness * 2f;
		}

		private void OnEnemyTargetRemovedEvent(ITargetable targetable)
		{
			if (enemies.ContainsKey(targetable))
			{
				enemies[targetable].Agent.DiedEvent -= OnEnemyDiedEvent;
				enemies.Remove(targetable);
			}
		}

		private void OnEnemyDiedEvent(DeathContext context)
		{
			// Enemy has died, satisfy all motivations towards them.
			IAgent enemy = context.Died;
			agent.Mind.Satisfy(Vector8.One * AEMOI.MAX_STIM, enemy);

			if (enemy != null && enemy.Targetable != null)
			{
				enemies[enemy.Targetable].Agent.DiedEvent -= OnEnemyDiedEvent;
				enemies.Remove(enemy.Targetable);
			}
		}

		#endregion Enemy Tracking

		#region Stimulation

		private void SendContinuousStimuli(float delta)
		{
			// Personality deviations [-1..1].
			Vector8 pers = agent.Mind.Personality;
			Vector8 dev = (pers - Vector8.Half) * 2f;

			// N–S axis balance and S-lean (for relaxing fight drive).
			float nsBalance = 1f - Mathf.Abs(dev.N - dev.S); // 0 = skewed, 1 = balanced.
			float nsLeanS = Mathf.Max(0f, dev.S - dev.N);  // >0 => more CAREFUL than FIERCE.

			foreach (EnemyInfo info in enemies.Values)
			{
				Vector8 current = agent.Mind.RetrieveStimuli(info.Agent);

				float threat01 = info.Threat;
				float threatStim = threat01 * AEMOI.MAX_STIM;
				float lethality01 = info.Lethality;
				float intent01 = info.Intent;
				float hate01 = Mathf.Clamp01(info.Resentment);
				float love01 = Mathf.Clamp01(-info.Resentment);

				// Resource deficits.
				float healthDef = statHandler.PointStats.SW.PercentageMax.Invert();
				float staminaDef = statHandler.PointStats.E.PercentageMax.Invert().Remap(-1f, 1f);
				float enduranceDef = statHandler.PointStats.W.PercentageMax.Invert();
				float resourceDef = Mathf.Clamp01(Mathf.Max(healthDef, Mathf.Max(staminaDef, enduranceDef)));
				float resourceOk = 1f - resourceDef;

				// Spike when enemy is winding up an attack on us.
				float windupDanger = 0f;
				if (info.Agent.Actor.State == PerformanceState.Preparing &&
					info.Agent.Actor.MainPerformer is IMovePerformer movePerformer &&
					movePerformer.Move is ICombatMove combatMove)
				{
					float range = combatMove.Range;
					if (combatMove is IMeleeCombatMove meleeCombatMove)
					{
						range += info.Agent.Stats.GetStat(AgentStatIdentifiers.REACH.SubStat(meleeCombatMove.Limb)) ?? 0f;
					}

					float t = Mathf.InverseLerp(range + range, range, info.Distance).InOutSine();
					windupDanger = t * AEMOI.MAX_STIM;
				}

				// Calm when threat & intent drop.
				float calm = (1f - threat01) * (1f - intent01);

				// Distance-based safety for S/E/W relaxation.
				float activeReach = info.CombatComp != null ? info.CombatComp.ActiveReach : 1f;
				float distanceSafe = Mathf.Clamp01(info.Distance / (activeReach * 2f)); // 0 = on top, 1 = far.
				float verySafe = calm * distanceSafe;

				// ---------------------------
				// Signed raw stim per axis
				// ---------------------------

				// N (Fight): danger minus relaxation.
				float fightDanger;
				{
					float courage = 1f - lethality01; // prefer to fight weaker/equal enemies.
					fightDanger = threatStim * hate01 * Mathf.Clamp01(courage + 0.3f) * (1f - 0.5f * resourceDef);
				}

				// Only really relax N if WATER (S) leans higher and axis is not extreme.
				float fightRelax = current.N * verySafe * nsBalance * nsLeanS * 1.25f;
				float fight = fightDanger - fightRelax;

				// NE (Utilize / anticipate opening).
				float utilizeDanger = info.Oppurtunity * AEMOI.MAX_STIM * (1f - 0.5f * threat01);
				float oppClosed = 1f - Mathf.Clamp01(Mathf.Abs(info.Oppurtunity)); // 1 when no clear opening.
				float neSafe = Mathf.Max(calm, oppClosed * distanceSafe);
				float utilizeRelax = current.NE * neSafe * 1.0f;
				float utilize = utilizeDanger - utilizeRelax;

				// E (Evade).
				float immediateThreat = threatStim * intent01;
				float evadeDanger = immediateThreat + windupDanger;

				// When far & calm, strongly relax evasion.
				float safeFromImmediate = verySafe;
				float evadeRelax = current.E * safeFromImmediate * 1.5f;
				float evade = evadeDanger - evadeRelax;

				// SE (Support) – left mostly to global systems.
				float support = 0f;

				// S (Retreat).
				float baseRetreat = threatStim * (0.4f + 0.6f * lethality01);
				float resourceFactor = 0.3f + 0.7f * resourceDef;
				float retreatDanger = baseRetreat * resourceFactor;
				float retreatSafe = distanceSafe * calm;
				float retreatRelax = current.S * retreatSafe * 1.5f; // stronger drop when far & calm.
				float retreat = retreatDanger - retreatRelax;

				// SW (Enhance / buffing).
				float enhanceDanger = AEMOI.MAX_STIM * lethality01 * (1f - threat01) * Mathf.Clamp01(resourceDef + 0.2f);
				float enhanceRelax = current.SW * resourceOk * calm * 1.0f;
				float enhance = enhanceDanger - enhanceRelax;

				// W (Guard).
				float guardDanger = threatStim * (0.25f + 0.75f * intent01) + windupDanger;
				float guardSafe = verySafe;
				float guardRelax = current.W * guardSafe * 1.5f;
				float guard = guardDanger - guardRelax;

				// NW (Target / aggro).
				float hateNorm = Mathf.Clamp01(info.Resentment);          // 0..1 hostility
				float maxContNW = Mathf.Max(info.Resentment, 1f);          // resentment or at least 1

				float nwDanger = threatStim * hateNorm;
				float nwRelax = love01 * AEMOI.MAX_STIM * calm;
				float target = nwDanger - nwRelax;                        // signed impulse

				float currentNW = current.NW;

				if (target > 0f)
				{
					if (currentNW >= maxContNW)
					{
						target = 0f;
					}
					else
					{
						float factor = Mathf.Clamp01((maxContNW - currentNW) / maxContNW);
						target *= factor;
					}
				}

				Vector8 rawStim = new Vector8(
					fight,    // N
					utilize,  // NE
					evade,    // E
					support,  // SE
					retreat,  // S
					enhance,  // SW
					guard,    // W
					target    // NW
				);

				// Aggro modulation (NW boosts positive combat impulses only).
				float aggroNorm = Mathf.Clamp01(current.NW / AEMOI.MAX_STIM);
				if (aggroNorm > 0f)
				{
					float strong = 1f + aggroNorm;        // 1..2
					float medium = 1f + 0.5f * aggroNorm; // 1..1.5

					ScalePositive(ref rawStim.N, strong);
					ScalePositive(ref rawStim.NE, strong);
					ScalePositive(ref rawStim.E, medium);
					ScalePositive(ref rawStim.S, medium);
					ScalePositive(ref rawStim.W, medium);
					ScalePositive(ref rawStim.SW, medium);
				}

				// Send raw impulses; AEMOI handles damping.
				agent.Mind.Stimulate(rawStim * delta, info.Agent);
			}
		}

		private void ScalePositive(ref float v, float factor)
		{
			if (v > 0f)
			{
				v *= factor;
			}
		}

		#endregion
	}
}
