using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AllySense : IDisposable
	{
		private readonly Dictionary<ITargetable, AllyInfo> allies = new Dictionary<ITargetable, AllyInfo>();

		private readonly IAgent agent;
		private readonly IVisionComponent vision;
		private readonly CombatSensesSettings settings;
		private readonly TargetingService targetingService;

		private readonly HashSet<ITargetable> visibleSet = new HashSet<ITargetable>();
		private readonly List<ITargetable> forgetBuffer = new List<ITargetable>(16);

		private string followingId;

		public AllySense(IAgent agent, IVisionComponent vision, CombatSensesSettings settings, TargetingService targetingService)
		{
			this.agent = agent;
			this.vision = vision;
			this.settings = settings;
			this.targetingService = targetingService;

			agent.Targeter.Allies.RemovedComponentEvent += OnAllyRemovedEvent;
		}

		public void Dispose()
		{
			agent.Targeter.Allies.RemovedComponentEvent -= OnAllyRemovedEvent;
		}

		public void Sense(float delta)
		{
			string newFollowingId = agent.RuntimeData.GetValue<string>(AgentDataIdentifiers.FOLLOWING);

			// If following was just cleared, immediately satisfy E for the previous follow target.
			if (!string.IsNullOrEmpty(followingId) && string.IsNullOrEmpty(newFollowingId))
			{
				foreach (AllyInfo info in allies.Values)
				{
					if (info.Agent.Identification.ID == followingId)
					{
						agent.Mind.Satisfy(new Vector8() { E = AEMOI.MAX_STIM }, info.Agent);
						break;
					}
				}
			}

			followingId = newFollowingId;
			GatherAllyData();
			SendContinuousStimuli(delta);
		}

		public AllyInfo GetAllyInfo(ITargetable targetable)
		{
			allies.TryGetValue(targetable, out AllyInfo info);
			return info;
		}

		#region Ally Tracking

		private void GatherAllyData()
		{
			List<ITargetable> allyList = agent.Targeter.Allies.Components;

			List<ITargetable> visible = vision.Spot(allyList);

			visibleSet.Clear();
			for (int i = 0; i < visible.Count; i++)
				visibleSet.Add(visible[i]);

			for (int i = 0; i < allyList.Count; i++)
			{
				ITargetable ally = allyList[i];

				if (ally is MonoBehaviour mb && !mb)
					continue;

				IAgent allyAgent = ally.Entity as IAgent;
				if (allyAgent == null || !allyAgent.Alive)
					continue;

				bool isFollowTarget = !string.IsNullOrEmpty(followingId) && allyAgent.Identification.ID == followingId;

				// Add to tracking if visible OR if this is the FOLLOWING entity.
				if (!allies.ContainsKey(ally) && !visibleSet.Contains(ally) && !isFollowTarget)
					continue;

				if (!allies.ContainsKey(ally))
				{
					allies.Add(ally, new AllyInfo(allyAgent));
					allyAgent.DiedEvent += OnAllyDiedEvent;
				}

				UpdateAllyInfo(ally, allies[ally]);
			}

			// Forget allies out of view for too long.
			forgetBuffer.Clear();

			foreach (KeyValuePair<ITargetable, AllyInfo> kv in allies)
			{
				ITargetable t = kv.Key;

				if (t is MonoBehaviour mb2 && !mb2)
				{
					forgetBuffer.Add(t);
					continue;
				}

				// Never forget the active FOLLOWING entity.
				if (kv.Value.IsFollowTarget)
					continue;

				if (!visibleSet.Contains(t) && Time.time - kv.Value.LastSeen > settings.AllyForgetTime)
					forgetBuffer.Add(t);
			}

			for (int i = 0; i < forgetBuffer.Count; i++)
			{
				ITargetable lost = forgetBuffer[i];
				if (lost != null && allies.TryGetValue(lost, out AllyInfo info))
				{
					info.Agent.DiedEvent -= OnAllyDiedEvent;
					agent.Mind.Satisfy(Vector8.One * AEMOI.MAX_STIM, info.Agent);
					allies.Remove(lost);
				}
				else
				{
					allies.Remove(lost);
				}
			}
		}

		private void UpdateAllyInfo(ITargetable ally, AllyInfo info)
		{
			info.IsFollowTarget = !string.IsNullOrEmpty(followingId) && info.Agent.Identification.ID == followingId;

			if (visibleSet.Contains(ally))
			{
				info.Visible = true;
				info.LastSeen = Time.time;
				info.LastLocation = info.Agent.Transform.position;

				Vector3 toAlly = info.Agent.Transform.position - agent.Transform.position;
				info.Distance = toAlly.magnitude;
				info.Direction = info.Distance > Mathf.Epsilon ? toAlly / info.Distance : Vector3.zero;
			}
			else
			{
				info.Visible = false;
			}
		}

		private void OnAllyRemovedEvent(ITargetable targetable)
		{
			if (allies.ContainsKey(targetable))
			{
				allies[targetable].Agent.DiedEvent -= OnAllyDiedEvent;
				allies.Remove(targetable);
			}
		}

		private void OnAllyDiedEvent(DeathContext context)
		{
			IAgent allyAgent = context.Died;
			agent.Mind.ClearStimuli(allyAgent);

			if (allyAgent?.Targetable != null)
			{
				allies[allyAgent.Targetable].Agent.DiedEvent -= OnAllyDiedEvent;
				allies.Remove(allyAgent.Targetable);
			}
		}

		#endregion Ally Tracking

		#region Stimulation

		private void SendContinuousStimuli(float delta)
		{
			float supportiveness = agent.Mind.Inclination.SE;

			foreach (AllyInfo info in allies.Values)
			{
				bool inCombat = info.CombatComp?.InCombatMode ?? false;
				bool isVulnerable = info.CombatComp?.IsVulnerable ?? false;
				float recentDmg = info.CombatComp?.RecentDamageNormalized ?? 0f;

				float healthRatio = 1f;
				{
					var sw = info.StatHandler?.PointStats.SW;
					if (sw != null && sw.Max > 0f)
						healthRatio = sw.Current / sw.Max;
				}

				Vector8 stim = Vector8.Zero;

				// E — Follow: fires for FOLLOWING target regardless of visibility; desire grows with distance.
				// Counter-pressure: satisfy E proportional to proximity so it doesn't accumulate during combat.
				if (info.IsFollowTarget)
				{
					float dist = Vector3.Distance(agent.Transform.position, info.Agent.Transform.position);
					float t = Mathf.Clamp01(dist / settings.FollowRange);
					stim.E = t;
					float proximity = 1f - t;
					if (proximity > 0f)
					{
						agent.Mind.Satisfy(new Vector8() { E = proximity * delta }, info.Agent);
					}
				}

				// N — Protect: ally taking damage or in combat.
				stim.N = Mathf.Clamp01(recentDmg + (inCombat ? 0.5f : 0f));

				// NE — Watch: ally in committed/vulnerable state.
				stim.NE = isVulnerable ? 1f : 0f;

				// SE — Rally: pull toward ally only when either is under active combat pressure (aggro stat > 0).
				bool enemyTargetingAlly = targetingService.IsBeingTargeted(info.Agent.Targetable);
				EntityStat selfAggroStat = agent.Stats.GetStat(AgentStatIdentifiers.AGGRO);
				EntityStat allyAggroStat = info.Agent.Stats.GetStat(AgentStatIdentifiers.AGGRO);
				bool selfInCombat = selfAggroStat != null && (float)selfAggroStat > 0f;
				bool allyInCombat = allyAggroStat != null && (float)allyAggroStat > 0f;
				if (selfInCombat || allyInCombat)
				{
					float ownMot = agent.Mind.Emotion.AbsSum();
					stim.SE = ownMot < settings.RallyMaxMotivation ? 1f - ownMot / settings.RallyMaxMotivation : 0f;
				}

				// S — Retreat-to: own fear seeks safe harbour near ally.
				float ownFear = agent.Mind.Emotion.S;
				stim.S = ownFear > settings.FearToRetreatThreshold
					? Mathf.Clamp01((ownFear - settings.FearToRetreatThreshold) / AEMOI.MAX_STIM) : 0f;

				// SW — Supply: ally health deficit.
				stim.SW = healthRatio < settings.SupplyHealthThreshold
					? 1f - healthRatio / settings.SupplyHealthThreshold : 0f;

				// W — Shield: ally low health and in danger.
				stim.W = healthRatio < settings.ShieldHealthThreshold && (inCombat || recentDmg > 0f)
					? 1f - healthRatio / settings.ShieldHealthThreshold : 0f;

				// NW — Push: ally being targeted or attacked; fires even before combat starts.
				stim.NW = Mathf.Clamp01(recentDmg * 2f + (inCombat ? 0.5f : 0f) + (enemyTargetingAlly ? 0.3f : 0f));

				// Apply supportiveness to all axes except E (E fires unconditionally for FOLLOWING).
				float followE = stim.E;
				stim *= supportiveness;
				stim.E = followE;

				agent.Mind.Stimulate(stim * delta, info.Agent); // POSITIVE — ally-directed
			}
		}

		#endregion Stimulation
	}
}
