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
		[SerializeField] private float chargePower = 2f;
		[SerializeField, MinMaxRange(0.5f, 1.5f)] private Vector2 speedModRange = new Vector2(0.5f, 1.5f);

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
		private EntityStat powerStat;
		private EntityStat limbOffenceStat;
		private EntityStat massStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;

		private FloatFuncModifier speedMod;

		private CombatHitDetector hitDetector;
		private TimerClass momentumTimer;
		private TimedCurveModifier hitPauseMod;

		private float totalCharge;

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

			timescaleStat = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(this.move.Limb));
			strengthStat = Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH);
			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			limbOffenceStat = Agent.Stats.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(this.move.Limb));
			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
			chargeStat = Agent.Stats.GetStat(move.ChargeCost.Stat);
			chargeSpeedStat = Agent.Stats.GetStat(move.ChargeSpeedMultiplierStat, false, 1f);
			performSpeedStat = Agent.Stats.GetStat(move.PerformSpeedMultiplierStat, false, 1f);
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
			totalCharge = 1f;

			speedMod = new FloatFuncModifier(ModMethod.Absolute, (float f) => f * (strengthStat / limbMassStat).Clamp(speedModRange.x, speedModRange.y));
			chargeSpeedStat.AddModifier(this, speedMod);
			performSpeedStat.AddModifier(this, speedMod);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;

			chargeSpeedStat.RemoveModifier(this);
			performSpeedStat.RemoveModifier(this);
			speedMod.Dispose();
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Performer.Prolong = RigidbodyWrapper.Speed > move.ProlongThreshold;

			if (Performer.State == PerformanceState.Preparing && Performer.Charge > Move.MinCharge)
			{
				// Drain charge stat.
				if (Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, move.ChargeCost.Cost * delta * chargeSpeedStat, true, out float damage, out bool drained))
				{
					totalCharge += damage * chargePower;
					if (drained)
					{
						Performer.TryPerform();
					}
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
					RigidbodyWrapper.PushRelative(move.Inertia * totalCharge);
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
				navigationHandler.TryGetClosestTarget(Agent.Targeter.Enemies.Components, false, out ITargetable closest, out float distance) &&
				distance < autoAimRange)
			{
				// Auto aim to closest targetable in range.
				rigidbodyWrapper.TargetVelocity = closest.Center - RigidbodyWrapper.Position;
			}
			movementHandler.ForceRotation();

			// STAT COST:
			if (massStat == null) SpaxDebug.Error("massStat NULL");
			if (limbMassStat == null) SpaxDebug.Error("limbMassStat NULL");

			Agent.Stats.TryApplyStatCost(Move.PerformCost.Stat, Move.PerformCost.Cost * (limbMassStat / strengthStat) * 100f, false, out _, out _);

			momentumTimer = new TimerClass(move.InertiaDelay, () => timescaleStat, callbackService);
		}

		protected void OnNewHitDetected(List<HitScanHitData> newHits)
		{
			foreach (HitScanHitData hit in newHits)
			{
				if (hit.GameObject.TryGetComponentRelative(out IHittable hittable))
				{
					// Generate hit data.
					Vector3 inertia = move.Inertia.Look((hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized) * totalCharge;
					float power = powerStat * move.Power * totalCharge;
					float offence = limbOffenceStat * move.Offence;

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Point,
						hit.Direction,
						limbMassStat,
						power,
						offence
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
				rigidbodyWrapper.Velocity *= 0.5f;

				if (chargeStat != null)
				{
					chargeStat.BaseValue += hitData.Result_Force * 0.001f;
				}

				if (hitData.Result_Parried)
				{
					Performer.TryCancel(true);
				}

				float hitPause = combatSettings.HitPauseReceiver.Lerp(hitData.Result_Impact * (1f / performSpeedStat.Value));
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
