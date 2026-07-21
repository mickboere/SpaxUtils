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
		private const float CLOSING_SMOOTH_RATE = 5f;    // ClosingSpeed EMA rate (higher = snappier; ~1/rate s time-constant).

		// Threat composition weights (should sum to 1).
		private const float THREAT_PROXIMITY_WEIGHT = 0.5f;
		private const float THREAT_LETHALITY_WEIGHT = 0.3f;
		private const float THREAT_INTENT_WEIGHT = 0.2f;

		// Lethality composition weights (should sum to 1).
		private const float LETHALITY_POINTS_WEIGHT = 0.5f;
		private const float LETHALITY_OFFENCE_WEIGHT = 0.3f;
		private const float LETHALITY_POWER_WEIGHT = 0.2f;

		// NE / Opportunity. TODO 14: calibrate all 8 from real frequency×magnitude data.
		private const float UTILIZE_WEIGHT = 0.5f;          // NE opportunity → danger scale (final magnitude).
		private const float OPPORTUNITY_RETREAT_SPEED = 2f; // enemy retreat speed (m/s) reading as a full "backing away" opening.
		private const float PERFORMING_OPPORTUNITY = 0.7f;  // a mid-swing (Performing) enemy is a PARTIAL opening — NE starts charging here so it can release in the recovery; Finishing (recovery) is the full opening.
		// Opportunity is the MAX of its weighted components (strongest single opening wins; weak ones don't stack). Tune each type's weight.
		private const float OPP_COMMITTED_WEIGHT = 1f;      // committed to a swing (Finishing = 1, Performing = PERFORMING_OPPORTUNITY)
		private const float OPP_STUNNED_WEIGHT = 1f;        // stunned
		private const float OPP_FACING_WEIGHT = 1f;         // back turned to us
		private const float OPP_BACKING_WEIGHT = 1f;        // retreating (overlaps NW/NE retreat cues — a candidate to lower)
		private const float OPP_RESOURCE_WEIGHT = 1f;       // out of defensive resources (can't guard/evade)

		// NW / Ruthless — pressure a STANDOFF: HOLD cue (steady) + lesser CHASE cue (retreating). N owns approach, NE owns retreat.
		private const float RUTHLESS_WEIGHT = 0.5f;         // NW standoff → danger scale (parallel to UTILIZE_WEIGHT).
		private const float NW_STANDOFF_SPEED = 2f;         // radial speed (m/s) that collapses the hold cue (enemy decisively charging/fleeing).
		private const float NW_RETREAT_WEIGHT = 0.33f;      // how much a fleeing enemy feeds NW vs NE (<1 = NE stays primary retreat response).
		private const float NW_HEALTH_BOOST = 1f;           // low health BOOSTS standoff (×(1 + healthDef·boost)) — hurt agents bait from range instead of trading. 1 = up to 2× at 0 health.

		// Hate empowers the whole offensive triad (N/NE/NW) as a shared GAIN, never an addend (×gain on a zero cue stays zero; cancels in neutral-balance).
		private const float HATE_GAIN = 1.5f;               // offensive drive = (1 + hate01·HATE_GAIN)×.

		// Storm-windup: over-charged attacks dash in from beyond melee range, so the melee gates read ~0 — spike danger directly (own storm-reach gate), fed to Evade + Guard.
		private const float STORM_WINDUP_FLOOR = 0.5f;      // immediate fraction of MAX_STIM when over-charging begins (grows toward MAX as the dash builds).

		// Finisher: as enemy health drops, press the kill — N + NW + a little NE. Aggression (Balance.N) presses always;
		// bloodlust (Balance.NW) wanes near own death. TODO 14: calibrate alongside the other 8 drives.
		private const float FINISHER_WEIGHT = 0.2f;         // near-death × (aggression+bloodlust) → up to ~2× this fraction of MAX_STIM on N & NW.
		private const float FINISHER_NE_SHARE = 0.5f;       // NE gets this fraction of the finisher (a little precision pull).
		private const float FINISHER_HEALTH_POWER = 4f;     // ease-in on health deficit: ~0 at half health (0.5^4≈0.06), bites near death.
		// SE / Mercy — Trust. A beaten (low-health) foe invokes the urge to spare; DENIED by hate (×(1-hate01)). Counters the finisher for the un-hated.
		private const float SE_MERCY_WEIGHT = 0.3f;         // enemy health-deficit → mercy fraction of MAX_STIM (linear).
		private const float RETREAT_WEIGHT = 0.5f;          // S fear/space-making scale (parallel to the other drive weights).
		private const float FINISHER_FALLOFF_POWER = 0.3f;  // <1 softens the shared distance falloff toward 1 (the chase reach); 1 = same as threat stim.
		#endregion Constants

		public event Action TrackedSetChanged;
		public IReadOnlyDictionary<ITargetable, EnemyInfo> TrackedEnemies => enemies;

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

		/// <summary>
		/// Forces this agent to start tracking <paramref name="hitter"/> as an enemy, bypassing the vision and region
		/// gates — being hit is unambiguous awareness, so an attacker striking from behind / off-screen must still be
		/// engageable (otherwise it's never tracked, the danger block never runs for it, and combat behaviours can't
		/// get an <see cref="EnemyInfo"/> to retaliate). Stamps <see cref="AgentTrackingInfo.LastSeen"/> so the forget
		/// pass doesn't drop a never-actually-seen attacker the very next frame; each subsequent hit refreshes it, and
		/// awareness then lingers for <c>ForgetTime</c> after the last hit. No-op for a non-agent / dead hitter; an
		/// already-tracked hitter just gets its awareness refreshed.
		/// </summary>
		public void ForceTrack(IAgent hitter)
		{
			if (hitter == null || !hitter.Alive)
			{
				return;
			}

			ITargetable targetable = hitter.Targetable;
			if (targetable == null || targetable == agent.Targetable)
			{
				return;
			}

			if (!enemies.TryGetValue(targetable, out EnemyInfo info))
			{
				info = new EnemyInfo(hitter);

				// Seed spatial data from the actual positions (mirrors UpdateEnemyInfo's visible branch). UpdateEnemyInfo
				// only refreshes Direction/Distance while the enemy is VISIBLE, so a never-yet-seen attacker would keep
				// the constructor default Vector3.zero — which consumers (Hostile's OrbitStrafer → LookRotation, the
				// movement InputAxis) reject with "viewing vector is zero" / "input axis cannot be zero". Seeding the
				// hit direction also makes the agent turn toward where it was struck; vision then takes over on the turn.
				Vector3 toHitter = hitter.Transform.position - agent.Transform.position;
				info.Distance = toHitter.magnitude;
				info.Direction = info.Distance > Mathf.Epsilon ? toHitter / info.Distance : agent.Transform.forward;
				info.LastLocation = hitter.Transform.position;

				enemies.Add(targetable, info);
				hitter.DiedEvent += OnEnemyDiedEvent;
				TrackedSetChanged?.Invoke();
			}

			info.LastSeen = Time.time;
		}

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

				if (enemyAgent == null || enemyAgent.Brain == null || !enemyAgent.Brain.IsStateActive(AgentStateIdentifiers.CONTROL) ||
					(!enemies.ContainsKey(enemy) && !visibleSet.Contains(enemy)))
				{
					// Enemy is not an agent, isn't autonomously active (asleep/cutscene/dead), or invisible and untracked; skip.
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
					TrackedSetChanged?.Invoke();
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
					TrackedSetChanged?.Invoke();
				}
				else
				{
					enemies.Remove(lostTargetable);
					TrackedSetChanged?.Invoke();
				}
			}
		}

		private void UpdateEnemyInfo(ITargetable enemy, EnemyInfo info, float delta)
		{
			// Resentment reflects current relations including any accumulated per-ID aggro.
			info.Resentment = -agent.Relations.Score(info.Agent.Identification);

			// Visibility.
			bool wasVisible = info.Visible;
			if (visibleSet.Contains(enemy))
			{
				info.Visible = true;
				info.LastSeen = Time.time;
				info.LastLocation = info.Agent.Transform.position;

				// Spatial.
				Vector3 toEnemy = info.Agent.Transform.position - agent.Transform.position;
				info.Distance = toEnemy.magnitude;
				info.Direction = info.Distance > Mathf.Epsilon ? toEnemy / info.Distance : Vector3.zero;

				// Relative velocity (enemy - self). ClosingSpeed > 0 when closing in. EMA-smoothed so a brief
				// feint/strafe can't spike it — this is the single stable closing-speed source every consumer
				// reads (anticipation lead + selection windup-risk). Snap to raw on first sight (no stale ramp).
				Vector3 relVel = info.Agent.Body.RigidbodyWrapper.Velocity - agent.Body.RigidbodyWrapper.Velocity;
				float rawClosing = -Vector3.Dot(relVel, info.Direction);
				info.ClosingSpeed = wasVisible
					? Mathf.Lerp(info.ClosingSpeed, rawClosing, Mathf.Clamp01(CLOSING_SMOOTH_RATE * delta))
					: rawClosing;
			}
			else
			{
				info.Visible = false;
			}

			// Lethality of enemy to agent.
			float enemyPointSum = info.StatHandler.PointStats.Vector8.Sum();
			float pointRatio = enemyPointSum <= Mathf.Epsilon ? 0.5f : enemyPointSum / pointSum;
			float powerRatio = info.CombatComp.Power / agent.Body.RigidbodyWrapper.Mass;

			float pointLeth = pointRatio / (pointRatio + 1f);
			// Offence lethality: expected physics damage the enemy's per-axis output (Offense) would deal
			// against OUR per-axis Defense (Armor/Yield mapping), relative to our health. This replaces
			// the legacy "Offense / Armor" — Armor only defends Slash (+half Power), not all output.
			float expectedDamage = combatComponent.EstimateIncomingDamage(info.CombatComp.Offense);
			float myMaxHealth = Mathf.Max(combatComponent.StatHandler.PointStats.SW.Max, 0.001f);
			float offenseLeth = expectedDamage / (expectedDamage + myMaxHealth);
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

			// Opportunity: enemy open to a precise/charged strike. A windup AGAINST us is NOT opportunity (that's
			// incoming danger → E/W/parry). Signals:
			//  - committed to a swing (Performing) or recovering (Finishing) — danger passing, charge for the recovery;
			//  - stunned; back turned;
			//  - backing away (their own retreat velocity — the honest, observable tell);
			//  - out of the resources to defend: low endurance (can't guard) and/or low stamina (can't evade).
			float facingAway = Mathf.Clamp01(info.Direction.NormalizedDot(info.Agent.Transform.forward));
			// Committed to a swing: Finishing (recovery) = full opening; Performing (mid-swing) = PARTIAL (NE starts
			// charging during the swing so it can release in the recovery, but a live swing is still dangerous).
			float committed = 0f;
			if (info.Agent.Actor != null)
			{
				if (info.Agent.Actor.State == PerformanceState.Finishing) committed = 1f;
				else if (info.Agent.Actor.State == PerformanceState.Performing) committed = PERFORMING_OPPORTUNITY;
			}
			float stunned = info.CombatComp != null && info.CombatComp.Stunned ? 1f : 0f;
			float backingAway = Mathf.Clamp01(
				Vector3.Dot(info.Agent.Body.RigidbodyWrapper.Velocity, info.Direction) / OPPORTUNITY_RETREAT_SPEED);
			float resourceOpenness = 0f;
			if (info.CombatComp != null && info.CombatComp.StatHandler != null)
			{
				// W = endurance (guard), E = stamina (evade). Open when even their best defense is depleted.
				float endFrac = info.CombatComp.StatHandler.PointStats.W.PercentageMax;
				float staFrac = info.CombatComp.StatHandler.PointStats.E.PercentageMax;
				resourceOpenness = 1f - Mathf.Max(endFrac, staFrac);
			}
			// MAX of the weighted opening types — the single strongest opening defines the opportunity (no stacking).
			info.Oppurtunity = Mathf.Clamp01(Mathf.Max(
				Mathf.Max(committed * OPP_COMMITTED_WEIGHT, stunned * OPP_STUNNED_WEIGHT),
				Mathf.Max(facingAway * OPP_FACING_WEIGHT,
					Mathf.Max(backingAway * OPP_BACKING_WEIGHT, resourceOpenness * OPP_RESOURCE_WEIGHT))));
		}

		private void OnEnemyTargetRemovedEvent(ITargetable targetable)
		{
			if (enemies.ContainsKey(targetable))
			{
				enemies[targetable].Agent.DiedEvent -= OnEnemyDiedEvent;
				enemies.Remove(targetable);
				TrackedSetChanged?.Invoke();
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
				TrackedSetChanged?.Invoke();
			}
		}

		#endregion Enemy Tracking

		#region Stimulation

		private void SendContinuousStimuli(float delta)
		{
			foreach (EnemyInfo info in enemies.Values)
			{
				// Ignore enemies that aren't autonomously active (asleep, in a cutscene, dead); drain toward them so we disengage.
				if (info.Agent.Brain == null || !info.Agent.Brain.IsStateActive(AgentStateIdentifiers.CONTROL))
				{
					agent.Mind.Satisfy(Vector8.One * delta, info.Agent);
					continue;
				}

				// When enemy leaves spawn region, flood-satisfy drives so they drain to zero,
				// allowing Hostile to win selection and return the agent.
				if (spawnpoint?.Region != null &&
					!spawnpoint.Region.IsInside(info.Agent.Transform.position))
				{
					agent.Mind.Satisfy(Vector8.One * delta, info.Agent);
					continue;
				}

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

				// Spike when enemy is winding up an attack on us.
				PerformanceState actorState = info.Agent.Actor.State;
				float windupDanger = 0f;
				float stormWindupDanger = 0f;   // over-charge spike, kept separate so it bypasses the melee-only gates below.
				bool storming = false;          // enemy is over-charging a storm-capable move at us.
				float stormPotentialReach = 0f; // max reach the building storm could attain (for the storm-aware falloff).
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

					// Over-charge (storm) escalation — see STORM_WINDUP_FLOOR. Only for a storm-capable move being
					// over-charged (ChargeMultiplier > 1 builds a real dash); carries its OWN storm-reach gate.
					if (info.CombatComp != null &&
						info.CombatComp.CurrentChargeMultiplier > 1f &&
						combatMove is IMeleeCombatMove stormMove && stormMove.StormDistance > 0f)
					{
						storming = true;
						// Gate by the storm's POTENTIAL reach (max it could fund) so danger registers from the instant
						// over-charging begins, even though the live dash is still short — they keep charging until it
						// reaches. Fades out past 2× as usual.
						stormPotentialReach = info.CombatComp.ProjectedStormReach;
						float stormProximity = Mathf.InverseLerp(stormPotentialReach + stormPotentialReach, stormPotentialReach, info.Distance).InOutSine();

						// Immediate floor, growing toward MAX_STIM as the held charge builds a longer live dash.
						float enemyActiveReach = info.CombatComp.ActiveReach;
						float dash = Mathf.Max(0f, info.CombatComp.CurrentStormReach - enemyActiveReach);
						float chargeGrowth = Mathf.Clamp01(dash / Mathf.Max(enemyActiveReach, 0.01f));
						stormWindupDanger = stormProximity * Mathf.Lerp(STORM_WINDUP_FLOOR, 1f, chargeGrowth) * AEMOI.MAX_STIM;
					}
				}

				// Approach danger: enemy closing within time horizon, gated by intent (facing + closing).
				float approachDanger = 0f;
				if (!float.IsPositiveInfinity(info.TimeToHit) && info.TimeToHit < settings.ApproachHorizonSeconds)
				{
					float tNorm = info.TimeToHit / settings.ApproachHorizonSeconds;
					approachDanger = (1f - tNorm).InOutSine() * intent01 * AEMOI.MAX_STIM;
				}

				// Distance-based safety: how far the enemy is relative to its reach (0 = on top, 1 = far). Used by SW (Enhance) distance-scaling.
				float activeReach = info.CombatComp != null ? info.CombatComp.ActiveReach : 1f;
				float distanceSafe = Mathf.Clamp01(info.Distance / (activeReach * 2f));

				// Reach proximity: 1 within reach, inversely proportional beyond.
				// Evade and guard only spike when the enemy can physically threaten us right now.
				float reachProximity = Mathf.Clamp01(activeReach / Mathf.Max(info.Distance, activeReach));

				// Hate (accrued contempt) = anger(N) + disgust(NW) — NEVER NE or S. NW takes it DIRECT (cold standoff);
				// N only as far as courage allows (hot charge, gated below); SE is SUPPRESSED by it (no mercy for the hated).
				// hateGain is the direct multiplier used by NW; N uses a courage-gated variant, SE uses (1-hate01). =1 at zero hate.
				float hateGain = 1f + hate01 * HATE_GAIN;

				// N (Fight/Anger): RAGE — acute anger at an in-reach/closing threat, gated by fear. courage = 1-lethality
				// (fear of being outmatched). Fear is also overcome by CURRENT aggression: Balance.N (dynamic anger, NOT
				// innate Personality) lets even a naturally-timid agent commit once their blood is up.
				float courage = 1f - lethality01;
				// Commit gate: a W/SW (endurance/health) deficit suppresses the urge to charge in — you can't afford to trade
				// blows you can't take — UNLESS current aggression (Balance.N) overrides it. Stamina (E) excluded (gates dashing, not fighting).
				float wswDeficit = Mathf.Max(enduranceDef, healthDef);
				float commitGate = Mathf.Lerp(1f - wswDeficit, 1f, agent.Mind.Balance.N);
				float rage = threatStim * Mathf.Clamp01(courage + 0.3f) * commitGate;
				// Hate feeds N (hot contempt → charge) ONLY as far as courage allows: an outmatched/fearful agent's hate stays
				// COLD and flows to NW instead of becoming a suicidal charge. Rage overcomes fear; hate does not.
				float fight = rage * (1f + hate01 * HATE_GAIN * courage);

				// NE (Utilize / precision charged strikes): driven by OPPORTUNITY only — the impulse to spend Static on a
				// strong charged hit when the enemy is open (committed / stunned / back-turned / RETREATING / resource-
				// open — so the "enemy opening the gap" regime already lives here). NOT amplified by windupDanger (that
				// responsiveness is the W/E tool); weighted below MAX_STIM so a full opening is on par with the threat
				// drives. NOT hate-empowered: NE is Anticipation (perceiving/timing an opening), not contempt — hate lives on N+NW.
				float utilize = info.Oppurtunity * AEMOI.MAX_STIM * UTILIZE_WEIGHT;

				// E (Evade): identical danger profile to Guard (W). The base stimulus is neutral/objective, so the
				// evade-vs-guard choice comes purely from the agent's inclination — not from a thumb on this scale.
				// Wind-up is reach-gated exactly like Guard.
				// stormWindupDanger is added OUTSIDE the melee reachProximity multiplier — it carries its own storm-reach gate.
				float evade = (threatStim * (0.25f + 0.75f * intent01) + windupDanger) * reachProximity + approachDanger * 0.5f + stormWindupDanger;

				// SE (Support/Mercy — Trust) — two cues: (1) CEDE when another agent already handles this enemy (crowding),
				// (2) MERCY toward a beaten foe (their health deficit invokes the urge to spare) — DENIED by hate: a hated foe
				// gets finished, not spared (so it counters the N/NW finisher only for the un-hated). Inclination.SE decides
				// whether the agent actually yields — cooperative yields, ruthless doesn't.
				int otherTargeters = Mathf.Max(0, targetingService.TargeterCount(info.Agent.Targetable) - 1);
				float crowding = Mathf.Clamp01(otherTargeters * 0.5f); // 0 alone, 0.5 one other, 1 two+
				float enemyHealthDef = info.StatHandler.PointStats.SW.PercentageMax.Invert(); // 1 = enemy at death's door
				float mercy = enemyHealthDef * (1f - hate01) * SE_MERCY_WEIGHT * AEMOI.MAX_STIM; // spare the beaten — unless hated
				float support = crowding * AEMOI.MAX_STIM * 0.5f + mercy;

				// Shared-target relaxation: drain all drives towards this enemy proportional to SE inclination.
				// Ruthless agents (low SE inclination) are unaffected; cooperative ones naturally cede.
				if (otherTargeters > 0)
				{
					float sharedRelax = Mathf.Clamp01(otherTargeters * settings.SharedTargetRelaxRate)
						* Mathf.Clamp01(agent.Mind.Inclination.SE) * delta;
					agent.Mind.Satisfy(Vector8.One * sharedRelax, info.Agent);
				}

				// S (Retreat): situational space-making, NOT constant fleeing. Scales with whichever is worse — how
				// outmatched we are (lethality) or how much our stats need recovering (resourceDef) — so a healthy,
				// evenly-matched agent doesn't retreat at all. No floor: the old 0.4/0.3 floors left even a fine agent
				// accruing a baseline (~0.12·threat) that tripped the lowered trigger. Max = "either reason is enough".
				float retreat = threatStim * Mathf.Max(lethality01, resourceDef) * RETREAT_WEIGHT;

				// SW (Enhance / buffing): self-regarding power-up urge — fire when OUTMATCHED and SAFE, scaling with DISTANCE
				// (distanceSafe: the more space from a strong foe, the stronger the urge). Health-only deficit for now
				// (stamina/endurance stripped). Emitted as its own term so it is EXEMPT from the foe distance falloff,
				// which would otherwise kill it exactly when the agent is safely far — the opposite of the intent.
				// SW settles at its demand level via the tracker (no pin-at-ceiling), so no drain term is needed even though SW has no satisfier behaviour yet.
				float enhance = AEMOI.MAX_STIM * lethality01 * (1f - threat01) * Mathf.Clamp01(healthDef + 0.2f) * distanceSafe;
				Vector8 enhanceStim = Vector8.SouthWest * enhance; // single-axis SW; combined foe-falloff-exempt at SetDemand.

				// W (Guard): gated by reach proximity — no pressure unless enemy is in engagement range.
				float guard = (threatStim * (0.25f + 0.75f * intent01) + windupDanger) * reachProximity + approachDanger * 0.5f + stormWindupDanger;

				// NW (Pierce/Pressure): pressure a SAFE STANDOFF. enemyRadial = enemy's OWN radial velocity (isolated from
				// ours): <0 approaching (N's job), ~0 holding (steady → pressure), >0 retreating (lesser chase, NE's tell
				// weighted down). `harmful` = the enemy winding up a strike at us — they plant, so steady would read 1, so
				// gate the cue by (1 - harmful) AND add harmful to the relax (opposite effect): under a windup NW stops
				// building and bleeds off, yielding to Guard/Evade. hateGain empowers; low health BOOSTS it (bait from range).
				float enemyRadial = Vector3.Dot(info.Agent.Body.RigidbodyWrapper.Velocity, info.Direction);
				float steady = 1f - Mathf.Clamp01(Mathf.Abs(enemyRadial) / NW_STANDOFF_SPEED);                  // 1 holding/jockeying, →0 on a decisive charge/flee
				float retreating = Mathf.Clamp01(Mathf.Max(0f, enemyRadial) / OPPORTUNITY_RETREAT_SPEED);       // 0 holding/closing, →1 as they flee (NE's cue)
				float harmful = Mathf.Clamp01((windupDanger + stormWindupDanger) / AEMOI.MAX_STIM);             // incoming windup (NOT mere proximity/recovery)
				float nwCue = Mathf.Clamp01(steady + retreating * NW_RETREAT_WEIGHT) * (1f - harmful);
				// Standoff is a RANGED bait/taunt game — the more hurt the agent, the MORE it should hover out and bait
				// (preserve resources for guarding) rather than trade. So low health BOOSTS it, opposite of a close attack.
				float targetNW = nwCue * AEMOI.MAX_STIM * RUTHLESS_WEIGHT * hateGain * (1f + healthDef * NW_HEALTH_BOOST);

				// Finisher: enemy near death → press the kill (N + NW + a little NE). Aggression (Balance.N) presses always;
				// bloodlust (Balance.NW) wanes near our own death; curved by FINISHER_HEALTH_POWER (only bites very low).
				// Kept OUT of rawStim so it carries its own gentler distance falloff (the chase) below.
				// (enemyHealthDef computed above for SE mercy.)
				float aggression = agent.Mind.Balance.N;                    // presses the kill regardless of own state
				float bloodlust = agent.Mind.Balance.NW * (1f - healthDef); // ruthless but not blind: wanes near death
				float finisher = enemyHealthDef.Pow(FINISHER_HEALTH_POWER) * (aggression + bloodlust) * AEMOI.MAX_STIM * FINISHER_WEIGHT;
				Vector8 finisherStim = new Vector8(
					finisher,                     // N
					finisher * FINISHER_NE_SHARE, // NE
					0f,                           // E
					0f,                           // SE
					0f,                           // S
					0f,                           // SW
					0f,                           // W
					finisher                      // NW
				);

				Vector8 rawStim = new Vector8(
					fight,    // N
					utilize,  // NE
					evade,    // E
					support,  // SE
					retreat,  // S
					0f,       // SW (self-regarding; emitted separately as enhanceStim below)
					guard,    // W
					targetNW  // NW
				);

				// Octology cascade: enemy's state at wheel position X drives our response at X+1 (clockwise).
				// Rotate(1) maps: enemy NW→our N, N→NE, NE→E, E→SE, SE→S, S→SW, SW→W, W→NW.
				// enemyBalance is 0-1 per axis; scale to stimulation space then apply.
				rawStim += enemyBalance.Rotate(1) * AEMOI.MAX_STIM * settings.CrossStateScale;

				// Foe-directed emotions are negative; flip sign before sending.
				// Exponential decay from attack range boundary: full signal within reach, sharp falloff beyond. While the
				// enemy is over-charging a storm the boundary extends to the storm's POTENTIAL reach, so the storm-windup
				// danger isn't decayed away before it can reach us (the whole point of reacting early to a building storm).
				float falloffReach = storming ? Mathf.Max(activeReach, stormPotentialReach) : activeReach;
				float beyondRange = Mathf.Max(0f, info.Distance - falloffReach);
				float distanceFalloff = settings.ExponentialFalloffK > 0f
					? Mathf.Exp(-settings.ExponentialFalloffK * beyondRange)
					: 1f;
				// The finisher reaches much further than close-range threat stim — once a predator smells weakness it
				// commits to the chase. Soften the PURE-distance falloff toward 1 with a sub-1 power, and take it from
				// before the actorState reduction so the enemy being mid-move doesn't dampen the urge to finish.
				float finisherFalloff = distanceFalloff.Pow(FINISHER_FALLOFF_POWER);

				distanceFalloff *= actorState switch
				{
					PerformanceState.Finishing  => 0.2f,
					PerformanceState.Performing => 0.8f,
					_                           => 1f,
				};
				// Continuous: this is the situational Demand LEVEL toward the foe (foe-directed → negative), not a per-frame impulse.
				agent.Mind.SetDemand(-(rawStim * distanceFalloff + finisherStim * finisherFalloff + enhanceStim), info.Agent);
			}
		}

		#endregion
	}
}
