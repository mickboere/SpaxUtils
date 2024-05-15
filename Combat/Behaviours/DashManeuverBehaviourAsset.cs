using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "Behaviour_Maneuver_Dash", menuName = "ScriptableObjects/Combat/DashManeuverBehaviourAsset")]
	public class DashManeuverBehaviourAsset : BasePerformanceMoveBehaviourAsset
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

			RigidbodyWrapper.Push(Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.MovementInput * dashSpeed);
		}

		public override void Stop()
		{
			base.Stop();
			callbackService.UnsubscribeUpdate(UpdateMode.FixedUpdate, this);
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

			if (State == PerformanceState.Preparing)
			{
				// Drain charge stat.
				ApplyStatCost(Move.ChargeCost, (massStat * RigidbodyWrapper.Speed + massStat * RigidbodyWrapper.Acceleration.magnitude) * delta, out bool drained);
				if (drained)
				{
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

		private void OnFixedUpdate()
		{
			Vector3 velocity = Quaternion.LookRotation(movementHandler.InputAxis) * movementHandler.MovementInput * glideSpeed;
			RigidbodyWrapper.ApplyMovement(velocity, controlForce, brakeForce, power, true);
			movementHandler.UpdateRotation(Time.fixedDeltaTime, null, true);
		}
	}
}
