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

		public void InjectDependencies(CallbackService callbackService, IAgentMovementHandler movementHandler)
		{
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;

			massStat = Agent.GetStat(AgentStatIdentifiers.MASS);
		}

		public override void Start()
		{
			base.Start();
			callbackService.SubscribeUpdate(UpdateMode.FixedUpdate, this, OnFixedUpdate);

			Vector3 startVelocity = movementHandler.MovementInputRaw == Vector3.zero ?
				RigidbodyWrapper.Forward * dashSpeed :
				Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.MovementInputRaw.normalized * dashSpeed;
			RigidbodyWrapper.Push(startVelocity);
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
				Agent.TryApplyStatCost(Move.ChargeCost, (massStat * RigidbodyWrapper.Speed + massStat * RigidbodyWrapper.Acceleration.magnitude) * delta, out bool drained);
				if (drained)
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

			weight = ((Performer.RunTime - Move.MinDuration) / Move.Release).ClampedInvert().InOutSine();
			weight *= (Performer.CancelTime / Move.CancelDuration).ClampedInvert();

			return instructions;
		}

		private void OnFixedUpdate()
		{
			if (RigidbodyWrapper.Speed < 0.1f)
			{
				// Exit dash.
				Performer.TryPerform();
				return;
			}

			Vector3 velocity = Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.MovementInputRaw * glideSpeed;
			RigidbodyWrapper.ApplyMovement(velocity, controlForce, brakeForce, power, true);
			movementHandler.UpdateRotation(Time.fixedDeltaTime, null, true);
		}
	}
}
