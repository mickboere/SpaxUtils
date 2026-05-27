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
		private readonly TargetingService targetingService;

		private float pointSum;
		private List<ITargetable> visible;

		private HashSet<ITargetable> visibleSet = new HashSet<ITargetable>();
		private List<ITargetable> forgetBuffer = new List<ITargetable>(16);

		private readonly ISpawnpoint spawnpoint;

		public EnemySense(
			IAgent agent,
			IVisionComponent vision,
			AgentStatHandler statHandler,
			AgentCombatComponent combatComponent,
			CombatSensesSettings settings,
			TargetingService targetingService,
			ISpawnpoint spawnpoint = null)
		{
			this.agent = agent;
			this.vision = vision;
			this.statHandler = statHandler;
			this.combatComponent = combatComponent;
			this.settings = settings;
			this.targetingService = targetingService;
			this.spawnpoint = spawnpoint;

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
					// Only begin tracking enemies inside this agent's assigned region.
					if (spawnpoint?.Region != null && !spawnpoint.Region.IsInside(enemyAgent.Transform.position))
						continue;

					enemyData = new EnemyInfo(enemyAgent);
					enemies.Add(enemy, enemyData);
					enemyAgent.DiedEvent += OnEnemyDiedEvent;
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
			// Resentment reflects current relations including any accumulated per-ID aggro.
			info.Resentment = -agent.Relations.Score(info.Agent.Identification);

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
			float offenseRatio = info.CombatComp.Offense / Mathf.Max(combatComponent.Proofing, 0.001f);
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
			if (info.ClosingSpeed > MIN_APPROACH_SPEED) rawIntent += 0.5f;
			if (info.CombatComp != null && info.CombatComp.CurrentCombatMove != null) rawIntent += 0.5f;
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
			// Enemy has died; hard-clear their stimuli so they can no longer influence behaviour evaluation.
			// (Satisfy only decays toward zero but leaves the dictionary entry, causing stale hostility.)
			IAgent enemy = context.Died;
			agent.Mind.ClearStimuli(enemy);

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
			float sCalm = agent.Mind.Balance.S; // live cautiousness: high S lean → relaxes fight drive

			foreach (EnemyInfo info in enemies.Values)
			{
				// When enemy leaves spawn region, flood-satisfy drives so they drain to zero,
				// allowing Hostile to win selection and return the agent.
				if (spawnpoint?.Region != null &&
					!spawnpoint.Region.IsInside(info.Agent.Transform.position))
				{
					agent.Mind.Satisfy(Vector8.One * delta, info.Agent);
					continue;
				}

				Vector8 current = agent.Mind.RetrieveStimuli(info.Agent);

				// Stimuli are now signed (negative = foe-directed); use absolute values when
				// reading current levels for relaxation calculations.
				Vector8 cur = current.Absolute();

				// Enemy's overall emotional balance (0-1 per axis) — observable demeanour.
				// Used by the cross-state cascade below; Balance is global, not target-specific.
				Vector8 enemyBalance = info.Agent.Mind.Balance;

				float threat01 = info.Threat;
				float threatStim = threat01 * AEMOI.MAX_STIM;
				float lethality01 = info.Lethality;
				float intent01 = info.Intent;
				// hate01: faction labels provide a baseline, personal aggro is the primary driver.
				// Score() sums ID entry + all labels; separate them so faction alone can't max NW.
				agent.Relations.Relations.TryGetValue(info.Agent.Identification.ID, out float idRelation);
				float personalHate = Mathf.Clamp01(-idRelation);                          // 0 on first contact, builds through hits
				float factionHate = Mathf.Clamp01(info.Resentment + idRelation);          // label-only contribution
				float hate01 = Mathf.Clamp01(factionHate * 0.3f + personalHate);          // faction = 30% ceiling, personal history = full driver

				// Resource deficits.
				float healthDef = statHandler.PointStats.SW.PercentageMax.Invert();
				float staminaDef = statHandler.PointStats.E.PercentageMax.Invert().Remap(-1f, 1f);
				float enduranceDef = statHandler.PointStats.W.PercentageMax.Invert();
				float resourceDef = Mathf.Clamp01(Mathf.Max(healthDef, Mathf.Max(staminaDef, enduranceDef)));
				float resourceOk = 1f - resourceDef;

				// Spike when enemy is winding up an attack on us.
				PerformanceState actorState = info.Agent.Actor.State;
				float windupDanger = 0f;
				if (actorState == PerformanceState.Preparing &&
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

				// Approach danger: enemy closing within time horizon, gated by intent (facing + closing).
				float approachDanger = 0f;
				if (!float.IsPositiveInfinity(info.TimeToHit) && info.TimeToHit < settings.ApproachHorizonSeconds)
				{
					float tNorm = info.TimeToHit / settings.ApproachHorizonSeconds;
					approachDanger = (1f - tNorm).InOutSine() * intent01 * AEMOI.MAX_STIM;
				}

				// Calm when threat & intent drop.
				float calm = (1f - threat01) * (1f - intent01);

				// Distance-based safety for S/E/W relaxation.
				float activeReach = info.CombatComp != null ? info.CombatComp.ActiveReach : 1f;
				float distanceSafe = Mathf.Clamp01(info.Distance / (activeReach * 2f)); // 0 = on top, 1 = far.
				float verySafe = calm * distanceSafe;

				// Reach proximity: 1 within reach, inversely proportional beyond.
				// Evade and guard only spike when the enemy can physically threaten us right now.
				float reachProximity = Mathf.Clamp01(activeReach / Mathf.Max(info.Distance, activeReach));

				// N (Fight/Anger): threat * courage * resources.
				// hate01 removed from danger (subjective); expressed through relax duration instead.
				// courage = 1-lethality is objective — fear of being outmatched reduces willingness to fight.
				float courage = 1f - lethality01;
				float fightDanger = threatStim * Mathf.Clamp01(courage + 0.3f) * (1f - 0.5f * resourceDef);
				// High N-inclination → N drains slowly (determined fighters stay angry longer).
				float fightRelax = cur.N * sCalm * Mathf.Lerp(verySafe * 1.25f + 0.2f, 0.05f, Mathf.Clamp01(agent.Mind.Inclination.N));
				float fight = fightDanger - fightRelax;

				// NE (Utilize / anticipate opening).
				float utilizeDanger = info.Oppurtunity * AEMOI.MAX_STIM * (1f - 0.5f * threat01);
				float oppClosed = 1f - Mathf.Clamp01(Mathf.Abs(info.Oppurtunity));
				float neSafe = Mathf.Max(calm, oppClosed * distanceSafe);
				// High NE-inclination → precision drive drains slowly (patient fighters hold the drive).
				float utilizeRelax = cur.NE * neSafe * Mathf.Lerp(1.0f, 0.1f, Mathf.Clamp01(agent.Mind.Inclination.NE));
				float utilize = utilizeDanger - utilizeRelax;

				// E (Evade): base danger gated by reach proximity; windupDanger is self-scaling via InverseLerp so no extra gate.
				float immediateThreat = threatStim * intent01;
				float evadeDanger = (immediateThreat + threatStim * 0.5f) * reachProximity + windupDanger + approachDanger * 0.5f;
				// Normalise total active threat 0-1; Clamp01 guards against simultaneous immediateThreat+windupDanger > MAX_STIM.
				float attackPressure = Mathf.Clamp01((immediateThreat + windupDanger) / AEMOI.MAX_STIM);
				// 1f = full-drain rate when no attack: cur.E * 1.0 * delta → exponential decay to zero. Lerps to verySafe during active threat.
				float evadeRelax = cur.E * Mathf.Lerp(1f, verySafe, attackPressure);
				float evade = evadeDanger - evadeRelax;

				// SE (Mercy) — back off when another agent is already handling this enemy.
				int otherTargeters = Mathf.Max(0, targetingService.TargeterCount(info.Agent.Targetable) - 1);
				float support = otherTargeters > 0 ? Mathf.Clamp01(otherTargeters * 0.5f) : 0f;

				// Shared-target relaxation: drain all drives towards this enemy proportional to SE inclination.
				// Ruthless agents (low SE inclination) are unaffected; cooperative ones naturally cede.
				if (otherTargeters > 0)
				{
					float sharedRelax = Mathf.Clamp01(otherTargeters * settings.SharedTargetRelaxRate)
						* Mathf.Clamp01(agent.Mind.Inclination.SE) * delta;
					agent.Mind.Satisfy(Vector8.One * sharedRelax, info.Agent);
				}

				// S (Retreat).
				float baseRetreat = threatStim * (0.4f + 0.6f * lethality01);
				float resourceFactor = 0.3f + 0.7f * resourceDef;
				float retreatDanger = baseRetreat * resourceFactor;
				float retreatRelax = cur.S * distanceSafe * calm * 1.5f;
				float retreat = retreatDanger - retreatRelax;

				// SW (Enhance / buffing).
				float enhanceDanger = AEMOI.MAX_STIM * lethality01 * (1f - threat01) * Mathf.Clamp01(resourceDef + 0.2f);
				float enhanceRelax = cur.SW * resourceOk * calm * 1.0f;
				float enhance = enhanceDanger - enhanceRelax;

				// W (Guard): gated by reach proximity — no pressure unless enemy is in engagement range.
				float guardDanger = (threatStim * (0.25f + 0.75f * intent01) + windupDanger) * reachProximity + approachDanger * 0.5f;
				// 1.5f: Guard drains 50% faster than Evade in safe conditions — sustained blocking should disengage sooner.
				float guardRelax = cur.W * Mathf.Lerp(1.5f, verySafe * 1.5f, attackPressure);
				float guard = guardDanger - guardRelax;

				// NW (Hate/Disgust/Relentlessness): driven by resentment as a slow-building emotional state.
				// hate01 (resentment) encodes accumulated relationship history — unlike N (anger), NW does
				// not spike with moment-to-moment threat. Scales to 30% of MAX_STIM at full hate so it is
				// meaningful but does not trivially dominate the trigger landscape.
				float nwDanger = hate01 * AEMOI.MAX_STIM * 0.3f;
				// High NW-inclination → NW drains slowly (relentless agents hold grudges longest).
				// Floor matches N's structure: low-inclination floor 0.2, high-inclination floor 0.05.
				float nwRelax = cur.NW * Mathf.Lerp(verySafe * 1.25f + 0.2f, verySafe * 0.25f + 0.05f, Mathf.Clamp01(agent.Mind.Inclination.NW));
				float targetNW = nwDanger - nwRelax;

				Vector8 rawStim = new Vector8(
					fight,    // N
					utilize,  // NE
					evade,    // E
					support,  // SE
					retreat,  // S
					enhance,  // SW
					guard,    // W
					targetNW  // NW
				);

				// Octology cascade: enemy's state at wheel position X drives our response at X+1 (clockwise).
				// Rotate(1) maps: enemy NW→our N, N→NE, NE→E, E→SE, SE→S, S→SW, SW→W, W→NW.
				// enemyBalance is 0-1 per axis; scale to stimulation space then apply.
				rawStim += enemyBalance.Rotate(1) * AEMOI.MAX_STIM * settings.CrossStateScale;

				// Foe-directed emotions are negative; flip sign before sending.
				// Exponential decay from attack range boundary: full signal within reach, sharp falloff beyond.
				float beyondRange = Mathf.Max(0f, info.Distance - activeReach);
				float distanceFalloff = settings.ExponentialFalloffK > 0f
					? Mathf.Exp(-settings.ExponentialFalloffK * beyondRange)
					: 1f;
				distanceFalloff *= actorState switch
				{
					PerformanceState.Finishing  => 0.2f,
					PerformanceState.Performing => 0.8f,
					_                           => 1f,
				};
				agent.Mind.Stimulate(-rawStim * delta * distanceFalloff, info.Agent);
			}
		}

		#endregion
	}
}
