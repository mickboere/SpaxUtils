using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="ICombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "Behaviour_MeleeAttack", menuName = "ScriptableObjects/Combat/MeleeAttackBehaviourAsset")]
	public class MeleeAttackingBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		[SerializeField] private float autoAimRange = 2f;
		[SerializeField] private LayerMask hitDetectionMask;

		protected ICombatMove combatMove;
		protected CallbackService callbackService;
		protected TransformLookup transformLookup;
		protected ITargeter targeter;
		protected IAgentMovementHandler movementHandler;
		protected AgentNavigationHandler navigationHandler;
		protected IEntityCollection entityCollection;
		protected CombatSettings combatSettings;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected ICommunicationChannel comms;

		private EntityStat timescaleStat;
		private EntityStat limbMassStat;
		private EntityStat strengthStat;
		private EntityStat limbOffenceStat;
		private EntityStat limbPiercingStat;
		private EntityStat massStat;

		private CombatHitDetector hitDetector;
		private bool wasPerforming;
		private TimerStruct momentumTimer;
		private bool appliedMomentum;
		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(ICombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection, CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper, ICommunicationChannel communicationChannel)
		{
			this.combatMove = move;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;
			this.targeter = targeter;
			this.movementHandler = movementHandler;
			this.navigationHandler = navigationHandler;
			this.entityCollection = entityCollection;
			this.combatSettings = combatSettings;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.comms = communicationChannel;

			timescaleStat = Agent.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = Agent.GetStat(AgentStatIdentifiers.MASS.SubStat(combatMove.Limb));
			strengthStat = Agent.GetStat(AgentStatIdentifiers.STRENGTH);
			limbOffenceStat = Agent.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(combatMove.Limb));
			limbPiercingStat = Agent.GetStat(AgentStatIdentifiers.PIERCING.SubStat(combatMove.Limb));
			massStat = Agent.GetStat(AgentStatIdentifiers.MASS);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, combatMove, hitDetectionMask);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

			if (Performer.State == PerformanceState.Preparing && Performer.Charge > Move.MinCharge)
			{
				// Drain charge stat.
				Agent.TryApplyStatCost(Move.ChargeCost, delta, out bool drained);
				if (drained)
				{
					Performer.TryPerform();
				}
			}

			if (Performer.State == PerformanceState.Performing)
			{
				if (!wasPerforming)
				{
					OnFirstFrameOfPerformance();
					wasPerforming = true;
				}

				if (Performer.RunTime >= combatMove.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					OnNewHitDetected(newHits);
				}

				// Apply momentum to user after delay.
				if (!appliedMomentum && !momentumTimer)
				{
					RigidbodyWrapper.PushRelative(combatMove.Inertia);
					appliedMomentum = true;
				}
			}
		}

		protected void OnFirstFrameOfPerformance()
		{
			// AIMING:
			if (targeter.Target != null)
			{
				// Auto aim to target.
				rigidbodyWrapper.TargetVelocity = targeter.Target.Center - RigidbodyWrapper.Position;
			}
			else if (RigidbodyWrapper.TargetVelocity.magnitude <= 1f &&
				navigationHandler.TryGetClosestTarget(Agent.Targeter.Enemies.Components, out ITargetable closest, out float distance) &&
				distance < autoAimRange)
			{
				// Auto aim to closest targetable in range.
				rigidbodyWrapper.TargetVelocity = closest.Center - RigidbodyWrapper.Position;
			}
			movementHandler.ForceRotation();

			// STAT COST:
			Agent.TryApplyStatCost(Move.PerformCost, (massStat - limbMassStat) * 0.5f + limbMassStat * 2f, out bool drained);
			// TODO?: If drained enter either tire or overheat state.

			momentumTimer = new TimerStruct(combatMove.ForceDelay);
			appliedMomentum = false;
		}

		protected void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					// Generate hit data.
					Vector3 inertia = combatMove.Inertia.Look((hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized);
					float mass = limbMassStat;
					float strength = strengthStat * combatMove.Strength;
					float offence = limbOffenceStat * combatMove.Offence;
					float piercing = limbPiercingStat * combatMove.Piercing;

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Direction,
						mass,
						strength,
						offence,
						piercing
					);

					ProcessAttack(hittable, hitData);
				}
			}
		}

		protected virtual void ProcessAttack(IHittable hittable, HitData hitData)
		{
			// Send hit and apply return data.
			if (hittable.Hit(hitData))
			{
				if (hitData.Result_Parried)
				{
					Performer.TryCancel(true);
				}

				float hitPause = combatSettings.MaxHitPause * hitData.Result_Penetration.InvertClamped();
				if (hitPauseMod == null || hitPause > hitPauseMod.Timer.Remaining)
				{
					// Apply hit-pause.
					hitPauseMod?.Dispose();
					hitPauseMod = new TimedCurveModifier(
						ModMethod.Absolute,
						combatSettings.HitPauseCurve,
						new TimerStruct(hitPause),
						callbackService);
					timescaleStat.RemoveModifier(this);
					timescaleStat.AddModifier(this, hitPauseMod);
				}

				comms.Send(hitData);
			}
		}
	}
}
