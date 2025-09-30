using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Behaviour_Maneuver_Dash", menuName = "ScriptableObjects/Combat/DashManeuverBehaviourAsset")]
	public class DashManeuverBehaviourAsset : CorePerformanceMoveBehaviourAsset
	{
		[SerializeField] private float dashSpeed = 10f;
		[SerializeField] private float glideSpeed = 5f;
		[SerializeField] private float controlForce = 1800f;
		[SerializeField] private float brakeForce = 900f;
		[SerializeField] private float power = 40f;

		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;

		private EntityStat massStat;
		private EntityStat movementSpeed;

		public void InjectDependencies(CallbackService callbackService, IAgentMovementHandler movementHandler)
		{
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;

			massStat = Agent.Stats.GetStat(AgentStatIdentifiers.MASS);
			movementSpeed = Agent.Stats.GetStat(AgentStatIdentifiers.MOVEMENT_SPEED);
		}

		public override void Start()
		{
			base.Start();
			callbackService.SubscribeUpdate(UpdateMode.FixedUpdate, this, OnFixedUpdate);

			Vector3 startVelocity = movementHandler.InputRaw == Vector3.zero ?
				RigidbodyWrapper.Forward * dashSpeed * (movementSpeed ?? 1f) :
				Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.InputRaw.normalized * dashSpeed * (movementSpeed ?? 1f);
			RigidbodyWrapper.Push(startVelocity);
			// TODO: Instead of push, animate the target velocity.
		}

		public override void Stop()
		{
			base.Stop();
			callbackService.UnsubscribeUpdate(UpdateMode.FixedUpdate, this);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				float cost = Move.ChargeCost.Cost * (massStat * RigidbodyWrapper.Speed + massStat * RigidbodyWrapper.Acceleration.magnitude) * delta;
				if (Agent.Stats.TryApplyStatCost(Move.ChargeCost.Stat, cost, false, out _, out bool drained) && drained)
				{
					// Exit dash.
					Performer.TryPerform();
				}
			}
		}

		protected override IPoserInstructions Evaluate(out float weight)
		{
			Vector3 input = RigidbodyWrapper.RelativeVelocity;
			IPoserInstructions instructions = Move.PosingData.GetInstructions(Performer.Charge, input);

			weight = ((Performer.RunTime - Move.MinDuration) / Move.Release).InvertClamped().InOutSine();
			weight *= (Performer.CancelTime / Move.CancelDuration).InvertClamped();

			return instructions;
		}

		private void OnFixedUpdate(float delta)
		{
			if (RigidbodyWrapper.Speed < 0.1f)
			{
				// Exit dash.
				Performer.TryPerform();
				return;
			}

			Vector3 velocity = Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.InputRaw * glideSpeed * (movementSpeed ?? 1f);
			RigidbodyWrapper.ApplyMovement(velocity, controlForce, brakeForce, power, true);
			movementHandler.UpdateRotation(delta, null, true);
		}
	}
}
