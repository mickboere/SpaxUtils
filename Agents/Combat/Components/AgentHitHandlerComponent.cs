using SpaxUtils.StateMachines;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling hits coming through in the IHittable component.
	/// </summary>
	public class AgentHitHandlerComponent : EntityComponentMono
	{
		public IReadOnlyDictionary<string, float> DamageLedger => damageLedger;

		public bool Invulnerable => agent.RuntimeData.GetValue(AgentDataIdentifiers.INVULNERABLE, false);

		[SerializeField, Tooltip("When enabled, hits landing toward this agent's back raise effective Vulnerability toward 1 (shaped by CombatSettings.RearExposureCurve), letting crits land from behind even through guard. When disabled, only the base Vulnerability stat is used regardless of hit angle.")]
		private bool backTurnWeakness = false;

		[SerializeField] private bool debug;

		private IAgent agent;
		private IHittable hittable;
		private RigidbodyWrapper rigidbodyWrapper;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IStunHandler stunHandler;
		private AgentStatHandler statHandler;

		private EntityStat timescaleStat;
		private EntityStat hardnessStat;
		private EntityStat vulnerabilityStat;
		private EntityStat proofingStat;
		private EntityStat pliancyStat;
		private EntityStat protectionStat;
		private EntityStat luckStat;

		private TimedCurveModifier hitPauseMod;

		private Dictionary<string, float> damageLedger = new Dictionary<string, float>();

		public void InjectDependencies(
			IAgent agent,
			IHittable hittable,
			RigidbodyWrapper rigidbodyWrapper,
			CombatSettings combatSettings,
			CallbackService callbackService,
			IStunHandler stunHandler,
			AgentStatHandler statHandler)
		{
			this.agent = agent;
			this.hittable = hittable;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
			this.stunHandler = stunHandler;
			this.statHandler = statHandler;
		}

		protected void OnEnable()
		{
			timescaleStat = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true);
			hardnessStat = agent.Stats.GetStat(AgentStatIdentifiers.HARDNESS, true);
			vulnerabilityStat = agent.Stats.GetStat(AgentStatIdentifiers.VULNERABILITY, true);
			proofingStat = agent.Stats.GetStat(AgentStatIdentifiers.PROOFING, true);
			pliancyStat = agent.Stats.GetStat(AgentStatIdentifiers.PLIANCY, true);
			protectionStat = agent.Stats.GetStat(AgentStatIdentifiers.PROTECTION, true);
			luckStat = agent.Stats.GetStat(AgentStatIdentifiers.LUCK, true);

			hittable.Subscribe(this, OnHitEvent, 100);
		}

		protected void OnDisable()
		{
			hittable.Unsubscribe(this);
		}

		private void OnHitEvent(HitData hitData)
		{
			bool blocked = hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED);
			bool parried = hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED);
			bool deflected = hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED);
			bool neglect = blocked || parried || deflected;

			// --- 1. CRIT LAYER ---
			// Rear exposure: hits landing toward the back raise effective Vulnerability toward 1. The angle of
			// the hitter relative to our facing (front=0, side=0.5, rear=1) is shaped by the CombatSettings curve,
			// then lerps the (guard-reduced) Vulnerability stat up toward 1. This keeps the rear exposed even
			// while guarding, since guard only lowers the stat we lerp up from.
			float vulnerability = vulnerabilityStat.Value;
			if (backTurnWeakness)
			{
				Vector3 toHitter = (hitData.Hitter.Transform.position - rigidbodyWrapper.Position).FlattenY().normalized;
				float rearParam = toHitter.NormalizedDot(rigidbodyWrapper.Forward).Invert();
				float rearExposure = Mathf.Clamp01(combatSettings.RearExposureCurve.Evaluate(rearParam));
				vulnerability = Mathf.Lerp(vulnerability, 1f, rearExposure);
			}

			float coupling = SpaxFormulas.CalculateCoupling(hitData.Precision, pliancyStat);
			float critChance = SpaxFormulas.CalculateCritChance(coupling, vulnerability, hitData.Luck, luckStat);
			bool isCrit = !neglect &&
				hitData.Precision > 0f &&
				Random.value < critChance;

			float critDamage = isCrit
				? SpaxFormulas.CalculateDamage(hitData.Precision, pliancyStat)
				: 0f;

			hitData.Data.SetValue(HitDataIdentifiers.CRIT, isCrit);
			hitData.Data.SetValue(HitDataIdentifiers.COUPLING, coupling);
			hitData.Data.SetValue(HitDataIdentifiers.CRIT_DAMAGE, critDamage);

			// --- 2. PIERCE LAYER ---
			float pierceDamage = 0f;
			float penetration = 0f;

			if (!neglect && hitData.Piercing > 0f)
			{
				// Proofing defends against Piercing.
				pierceDamage = SpaxFormulas.CalculateDamage(hitData.Piercing, proofingStat);

				// Penetration is defined by how much of the incoming piercing becomes actual piercing damage.
				penetration = Mathf.Clamp01(pierceDamage / hitData.Piercing);
			}

			hitData.Data.SetValue(HitDataIdentifiers.PENETRATION, penetration);
			hitData.Data.SetValue(HitDataIdentifiers.PIERCING_DAMAGE, pierceDamage);

			// --- 3. BLUNT LAYER ---
			float impact = 0f;
			float bluntDamage = 0f;

			if (!neglect && hitData.Power > 0f)
			{
				// Power sits at the centre of the octad, guarded by BOTH proofing and pliancy. It always
				// transmits some blunt through the target's rigidity (hardness), increased when the hit stays
				// blunt (low penetration) and when it connects cleanly (coupling). Penetration and coupling are
				// already defended upstream (by proofing and pliancy respectively), so the wall here is the full
				// proofing + pliancy - neither stat alone can ever fully negate the centre.
				float bluntOffence = hitData.Power * (hardnessStat + (1f - penetration) + coupling);
				bluntDamage = SpaxFormulas.CalculateDamage(bluntOffence, proofingStat + pliancyStat);

				// Normalised concussive transfer (0-1): the fraction of Power that landed as blunt. Reused for
				// force, hit-pause and audio, so it's clamped to a clean 0-1 against very low-defence targets.
				impact = Mathf.Clamp01(bluntDamage / hitData.Power);
			}

			hitData.Data.SetValue(HitDataIdentifiers.IMPACT, impact);
			hitData.Data.SetValue(HitDataIdentifiers.BLUNT_DAMAGE, bluntDamage);

			// --- 4. TOTAL PHYSICS DAMAGE ---
			float totalDamage = critDamage + pierceDamage + bluntDamage;

			// --- 5. GRACE INTERVENTION ---
			// Applied AFTER physics calculation. It absorbs damage, it does not act as armor.
			if (totalDamage > 0f)
			{
				// Drain Grace
				float drained = statHandler.PointStats.SE.Drain(totalDamage);
				hitData.Data.SetValue(HitDataIdentifiers.GRACE, drained);

				// Reduce final damage by amount successfully drained from Grace
				totalDamage -= drained;
			}

			hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_TOTAL, totalDamage);

			// --- IMPACT & FORCE ---
			float force = hitData.Mass * hitData.Power * impact;
			hitData.Data.SetValue(HitDataIdentifiers.FORCE, force);

			// --- ENDURANCE DAMAGE ---
			float toEndure = neglect ? 0f : totalDamage + force;
			float enduranceDamage = statHandler.PointStats.W.Drain(
				toEndure,
				out bool stunned,
				out float enduranceOverdraw);
			hitData.Data.SetValue(HitDataIdentifiers.STUNNED, stunned);

			enduranceOverdraw *= toEndure / (enduranceDamage + enduranceOverdraw).Max(1f);
			float endured = toEndure > 0f ? (toEndure - enduranceOverdraw) / toEndure : 1f;
			hitData.Data.SetValue(HitDataIdentifiers.ENDURED, endured);

			// --- STUN / IMPACT APPLICATION ---
			if (stunned)
			{
				agent.Actor.TryCancel(true);
				rigidbodyWrapper.ResetVelocity();
				rigidbodyWrapper.Push(hitData.Direction * force * endured.Invert(), 1f);
				stunHandler.EnterStun(hitData);
			}
			else if (neglect)
			{
				rigidbodyWrapper.Push(hitData.Direction * force, 1f);
			}

			// --- INERTIA SHARING (clash) ---
			// Treat contact as a collision along the horizontal contact normal: the hitter's closing
			// momentum is shared by mass. CombatSettings.Restitution sets the elasticity — 0 = perfectly
			// inelastic (both end at the shared velocity, freezing the gap), 1 = fully elastic (they bounce
			// apart). Both the receiver's gain and the hitter's brake scale by (1 + restitution). Landed
			// hits only — a block/parry/deflect already arrests the attacker (ResetVelocity), so no creep there.
			if (!neglect)
			{
				Vector3 normal = (rigidbodyWrapper.Position - hitData.Hitter.Transform.position)
					.FlattenY().normalized;
				float closing = Mathf.Max(0f, Vector3.Dot(hitData.Inertia, normal));
				if (closing > 0f)
				{
					float totalMass = hitData.HitterMass + rigidbodyWrapper.Mass;
					float elasticity = 1f + combatSettings.Restitution;
					float receiverShare = hitData.HitterMass / totalMass * elasticity;
					float hitterShare = rigidbodyWrapper.Mass / totalMass * elasticity;

					// Receiver is brought up to the post-collision velocity along the contact normal.
					rigidbodyWrapper.Push(normal * (closing * receiverShare));

					// Hitter sheds its share of the closing velocity (applied on the hitter's side in ProcessHit).
					hitData.Data.SetValue(HitDataIdentifiers.INERTIA_BRAKE, -normal * (closing * hitterShare));
				}
			}

			// --- HP DAMAGE & MALICE ---
			if (!Invulnerable)
			{
				// Bracing (guard) trades health for stance: the blunt that would bleed health is borne entirely
				// by endurance instead (which already absorbed it via toEndure above), scaled by guard weight.
				// Pierce and crit are untouched - only a shield (raising proofing/pliancy) defends those.
				float guardWeight = Mathf.Clamp01(hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT));
				float healthDamage = Mathf.Max(0f, totalDamage - bluntDamage * guardWeight);

				float damageDealt = statHandler.PointStats.SW.Drain(healthDamage, out bool dead, out _);
				hitData.Data.SetValue(HitDataIdentifiers.DAMAGE_DEALT, damageDealt);

				// --- MALICE BUILDUP ---
				if (damageDealt > 0f &&
					hitData.Hitter != null &&
					hitData.Hitter is IAgent)
				{
					// Only builds if HP was actually lost (Grace prevents Malice gain)
					statHandler.PointStats.NW.Gain(damageDealt);
				}

				if (dead)
				{
					stunHandler.EnterStun(hitData, 5f);
					DeathContext context = new DeathContext(agent, hitData.Hitter, "Hit");
					agent.Die(context);
				}
			}

			// Build Static (NE): reward the defender for actively defending. A parry/deflect refunds the most
			// (it's the prime opening-creator for charged counters), a block half. Threat = the attack's POTENTIAL
			// force (Mass × Power) — the resolved force is 0 on a neglected (parried/blocked) hit. Landing a hit
			// rewards the attacker instead (handled in MeleeCombatBehaviourAsset.ProcessHit).
			float staticThreat = hitData.Mass * hitData.Power * combatSettings.StaticGain;
			if (parried || deflected)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat;
			}
			else if (blocked)
			{
				statHandler.PointStats.NE.Current.BaseValue += staticThreat * 0.5f;
			}
			else
			{
				// Partial guard: guard up but not a full block — still pays out at the block tier, scaled by how
				// much guard absorbed the hit.
				float guardWeight = hitData.Data.GetValue<float>(HitDataIdentifiers.GUARD_WEIGHT);
				if (guardWeight > 0f)
				{
					statHandler.PointStats.NE.Current.BaseValue += staticThreat * 0.5f * Mathf.Clamp01(guardWeight);
				}
			}

			// --- HIT PAUSE ---
			hitPauseMod?.Dispose();
			hitPauseMod = new TimedCurveModifier(
				ModMethod.Absolute,
				combatSettings.HitPauseCurve,
				new TimerStruct(combatSettings.HitPauseReceiver.Lerp(impact) * endured.InvertClamped()),
				callbackService);

			timescaleStat.RemoveModifier(this);
			timescaleStat.AddModifier(this, hitPauseMod);

			// Update ledger.
			if (damageLedger.ContainsKey(hitData.Hitter.ID))
			{
				damageLedger[hitData.Hitter.ID] += totalDamage;
			}
			else
			{
				damageLedger[hitData.Hitter.ID] = totalDamage;
			}

			if (debug)
			{
				SpaxDebug.Log($"{agent.ID} - HIT:", hitData.ToString() + "\nEntity Stats:\n" + Entity.Stats.GetSnapshot());
			}
		}
	}
}
