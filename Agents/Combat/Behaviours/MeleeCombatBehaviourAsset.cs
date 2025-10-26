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
		[SerializeField, MinMaxRange(0.5f, 1.5f)] private Vector2 strengthSpeedModRange = new Vector2(0.7f, 1.15f);

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
		protected IStunHandler stunHandler;
		protected AgentStatHandler statHandler;
		protected AwarenessComponent awarenessComponent;

		private EntityStat timescaleStat;
		private EntityStat limbMassStat;
		private EntityStat strengthStat;
		private EntityStat powerStat;
		private EntityStat limbOffenceStat;
		private EntityStat chargeStat;
		private EntityStat chargeSpeedStat;
		private EntityStat performSpeedStat;
		private EntityStat enduranceCostStat;

		private FloatFuncModifier speedMod;
		private FloatOperationModifier balanceMod;

		private CombatHitDetector hitDetector;
		private TimerClass momentumTimer;
		private TimedCurveModifier hitPauseMod;

		private float totalCharge;

		public void InjectDependencies(IMeleeCombatMove move, CallbackService callbackService,
			TransformLookup transformLookup, ITargeter targeter, IAgentMovementHandler movementHandler,
			AgentNavigationHandler navigationHandler, IEntityCollection entityCollection, CombatSettings combatSettings,
			RigidbodyWrapper rigidbodyWrapper, ICommunicationChannel comms, IStunHandler stunHandler,
			AgentStatHandler statHandler, AwarenessComponent awarenessComponent)
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
			this.comms = comms;
			this.stunHandler = stunHandler;
			this.statHandler = statHandler;
			this.awarenessComponent = awarenessComponent;

			timescaleStat = Agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
			limbMassStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS.SubStat(this.move.Limb));
			strengthStat = Agent.Stats.GetStat(AgentStatIdentifiers.STRENGTH);
			powerStat = Agent.Stats.GetStat(AgentStatIdentifiers.POWER);
			limbOffenceStat = Agent.Stats.GetStat(AgentStatIdentifiers.OFFENCE.SubStat(this.move.Limb));
			chargeStat = Agent.Stats.GetStat(move.ChargeCost.Stat);
			chargeSpeedStat = Agent.Stats.GetStat(move.ChargeSpeedMultiplierStat, false, 1f);
			performSpeedStat = Agent.Stats.GetStat(move.PerformSpeedMultiplierStat, false, 1f);
			enduranceCostStat = Agent.Stats.GetStat(AgentStatIdentifiers.ENDURANCE.SubStat(AgentStatIdentifiers.SUB_COST));
		}

		public override void Start()
		{
			base.Start();

			hitDetector = new CombatHitDetector(Agent, transformLookup, move, hitDetectionMask);
			Performer.StartedPerformingEvent += OnPerformanceStartedEvent;
			totalCharge = 1f;

			speedMod = new FloatFuncModifier(ModMethod.Absolute, (float f) => f * (strengthStat / limbMassStat).Clamp(strengthSpeedModRange.x, strengthSpeedModRange.y));
			chargeSpeedStat.AddModifier(this, speedMod);
			performSpeedStat.AddModifier(this, speedMod);
			balanceMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			enduranceCostStat.AddModifier(this, balanceMod);
		}

		public override void Stop()
		{
			base.Stop();

			hitDetector.Dispose();
			Performer.StartedPerformingEvent -= OnPerformanceStartedEvent;

			chargeSpeedStat.RemoveModifier(this);
			performSpeedStat.RemoveModifier(this);
			speedMod.Dispose();
			enduranceCostStat.RemoveModifier(this);
			balanceMod.Dispose();
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			Performer.Prolong = RigidbodyWrapper.Speed > move.ProlongThreshold;

			if (Performer.State == PerformanceState.Preparing && Performer.Charge > Move.MinCharge)
			{
				// Overcharging: Drain charge stat.
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
				// TODO: Create homing system that pulls and steers agent towards enemy, performing attack once in range.
				if (momentumTimer != null && momentumTimer.Expired)
				{
					RigidbodyWrapper.PushRelative(move.Inertia * totalCharge);
					momentumTimer.Dispose();
					momentumTimer = null;
				}
			}

			float balance = Performer.State == PerformanceState.Preparing ? move.ChargeBalance : move.PerformBalance;
			balanceMod.SetValue((1f / balance).Lerp(1f, Weight.Invert()));
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
				// TODO: Utilize attack range instead of autoAimRange.
				rigidbodyWrapper.TargetVelocity = closest.Center - RigidbodyWrapper.Position;
			}
			movementHandler.ForceRotation();

			// STAT COST:
			if (limbMassStat == null) SpaxDebug.Error("limbMassStat NULL");
			if (strengthStat == null) SpaxDebug.Error("strengthStat NULL");

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
					Vector3 lookDir = (hittable.Entity.Transform.position - Agent.Transform.position).FlattenY().normalized;
					Vector3 inertia = move.Inertia.Look(lookDir) * totalCharge;
					Vector3 direction = move.CustomDirection ? move.HitDirection.Look(lookDir) : hit.Direction;
					float power = powerStat * move.Power * totalCharge;
					float offence = limbOffenceStat * move.Offence;

					HitData hitData = new HitData(
						hittable,
						Agent,
						rigidbodyWrapper.Mass,
						inertia,
						hit.Point,
						direction,
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
				float force = hitData.Data.GetValue<float>(HitDataIdentifiers.FORCE);

				rigidbodyWrapper.AddForce(-rigidbodyWrapper.Velocity * 0.5f, ForceMode.VelocityChange);

				if (chargeStat != null)
				{
					chargeStat.BaseValue += force * 0.001f;
				}

				if (hitData.Data.GetValue<bool>(HitDataIdentifiers.BLOCKED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					stunHandler.EnterStun(hitData, combatSettings.BlockedStunTime);
				}
				else if (hitData.Data.GetValue<bool>(HitDataIdentifiers.PARRIED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					statHandler.PointStats.W.Current.BaseValue = 0f; // Drain endurance.
					stunHandler.EnterStun(hitData, combatSettings.ParriedStunTime);
				}
				else if (hitData.Data.GetValue<bool>(HitDataIdentifiers.DEFLECTED))
				{
					Performer.TryCancel(true);
					rigidbodyWrapper.ResetVelocity();
					statHandler.PointStats.W.Current.BaseValue = 0f; // Drain endurance.
					rigidbodyWrapper.Push(-hitData.Direction * force, 1f); // Share force.
					stunHandler.EnterStun(hitData, combatSettings.DeflectedStunTime);
				}

				float hitPause = combatSettings.HitPauseReceiver.Lerp(hitData.Data.GetValue<float>(HitDataIdentifiers.IMPACT) * (1f / performSpeedStat.Value));
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
				awarenessComponent.ReportImpact(new ImpactData()
				{
					Source = Agent,
					Victim = hitData.Receiver.Entity,
					HitObject = hitData.Receiver.Entity.GameObject,
					Location = hitData.Point,
					Force = hitData.Data.GetValue(HitDataIdentifiers.FORCE, 1f),
					Direction = hitData.Direction
				});
			}
		}
	}
}
