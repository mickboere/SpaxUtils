using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Behaviour for melee <see cref="IMeleeCombatMove"/>s that manages hit-detection during performance.
	/// </summary>
	[CreateAssetMenu(fileName = "CombatBehaviour_MeleeAttack", menuName = "ScriptableObjects/Combat/MeleeCombatBehaviourAsset")]
	public class MeleeCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		[SerializeField] private float autoAimRange = 2f;
		[SerializeField] private LayerMask hitDetectionMask;

		protected IMeleeCombatMove move;
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
		private TimerClass momentumTimer;
		private TimedCurveModifier hitPauseMod;

		public void InjectDependencies(IMeleeCombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection, CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper, ICommunicationChannel communicationChannel)
		{
			this.move = move;
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
			limbMassStat = Agent.GetStat(AgentStatIdentifiers.MASS.SubStat(this.move.Limb));
			strengthStat = Agent.GetStat(AgentStatIdentifiers.STRENGTH);
			limbOffenceStat = Agent.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(this.move.Limb));
			limbPiercingStat = Agent.GetStat(AgentStatIdentifiers.PIERCING.SubStat(this.move.Limb));
			massStat = Agent.GetStat(AgentStatIdentifiers.MASS);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

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
				if (Performer.RunTime >= move.HitDetectionDelay && hitDetector.Update(out List<HitScanHitData> newHits))
				{
					OnNewHitDetected(newHits);
				}

				// Apply momentum to user after delay.
				if (momentumTimer != null && momentumTimer.Expired)
				{
					RigidbodyWrapper.PushRelative(move.Inertia);
					momentumTimer.Dispose();
					momentumTimer = null;
				}
			}
		}

		protected void OnPerformanceStartedEvent(IPerformer performer)
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
			// TODO?: If drained enter either tire or overheat state?

			momentumTimer = new TimerClass(move.ForceDelay, () => timescaleStat, callbackService);
		}

		protected void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					// Generate hit data.
					Vector3 inertia = move.Inertia.Look((hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized);
					float mass = limbMassStat;
					float strength = strengthStat * move.Strength;
					float offence = limbOffenceStat * move.Offence;
					float piercing = limbPiercingStat * move.Piercing;

					HitData hitData = new HitData(
						hittable,
						Agent,
						hit.Point,
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
