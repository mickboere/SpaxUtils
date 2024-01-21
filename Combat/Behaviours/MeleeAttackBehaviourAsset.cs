using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Behaviour_MeleeAttack", menuName = "ScriptableObjects/MeleeAttackBehaviourAsset")]
	public class MeleeAttackBehaviourAsset : BehaviourAsset
	{
		private ICombatPerformer performer;
		private ICombatMove move;
		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;

		public void InjectDependencies(ICombatPerformer performer, ICombatMove move,
			IAgent agent, RigidbodyWrapper rigidbodyWrapper)
		{
			this.performer = performer;
			this.move = move;
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		public override void Start()
		{
			base.Start();
			performer.NewHitDetectedEvent += OnNewHitDetectedEvent;
		}

		public override void Stop()
		{
			base.Stop();
			performer.NewHitDetectedEvent -= OnNewHitDetectedEvent;
		}

		private void OnNewHitDetectedEvent(List<HitScanHitData> newHits)
		{
			// TODO: Have hit pause duration depend on penetration % (factoring in power, sharpness, hardness.)
			//timeMod = new TimedCurveModifier(ModMethod.Absolute, hitPauseCurve, new Timer(hitPause), callbackService);

			bool successfulHit = false;
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					Vector3 inertia = move.Inertia.Look((hittable.Entity.Transform.position - agent.Transform.position).FlattenY());

					// Calculate attack force.
					float strength = 0f;
					if (agent.TryGetStat(performer.CurrentMove.StrengthStat, out EntityStat strengthStat))
					{
						strength = strengthStat;
					}

					float force = rigidbodyWrapper.Mass + strength;

					// Generate hit-data for hittable.
					HitData hitData = new HitData(
						agent,
						hittable,
						inertia,
						force,
						hit.Direction,
						false,
						new Dictionary<string, float>()
					);

					// If move is offensive, add base health damage to HitData.
					if (performer.CurrentMove.Offensive &&
						agent.TryGetStat(performer.CurrentMove.OffenceStat, out EntityStat offence) &&
						hittable.Entity.TryGetStat(AgentStatIdentifiers.DEFENCE, out EntityStat defence))
					{
						float damage = SpaxFormulas.GetDamage(offence, defence) * performer.CurrentMove.Offensiveness;
						hitData.Damages.Add(AgentStatIdentifiers.HEALTH, damage);
					}

					// Invoke hit event to allow adding of additional damage.
					//ProcessHitEvent?.Invoke(hitData);

					if (hittable.Hit(hitData))
					{
						successfulHit = true;

						// Apply hit pause to enemy.
						// TODO: Must be applied on enemy's end.
						//EntityStat hitTimeScale = hittable.Entity.GetStat(EntityStatIdentifier.TIMESCALE);
						//if (hitTimeScale != null)
						//{
						//	hitTimeScale.AddModifier(this, timeMod);
						//}
					}
				}
			}

			//if (successfulHit)
			//{
			//	EntityTimeScale.RemoveModifier(this);
			//	EntityTimeScale.AddModifier(this, timeMod);
			//}
		}
	}
}
