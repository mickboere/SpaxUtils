using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Entity component responsible for handling agent maneuvers.
	/// </summary>
	public class AgentManeuvererComponent : EntityComponentBase
	{
		[SerializeField] private float controlForce = 1800f;
		[SerializeField] private float brakeForce = 900f;
		[SerializeField] private float power = 40f;

		[SerializeField] private PoseSequenceBlendTree leanPose;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private CombatSettings combatSettings;
		private CallbackService callbackService;
		private IAgentMovementHandler movementHandler;

		private FloatOperationModifier controlMod;
		private bool maneuvering;

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper,
			AnimatorPoser animatorPoser, CombatSettings combatSettings,
			CallbackService callbackService, IAgentMovementHandler movementHandler)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
			this.movementHandler = movementHandler;
		}

		protected void OnEnable()
		{
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
		}

		protected void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
			controlMod.Dispose();
		}

		protected void FixedUpdate()
		{
			if (maneuvering)
			{
				rigidbodyWrapper.ApplyMovement(controlForce, brakeForce, power, true);
				Vector3 input = rigidbodyWrapper.RelativeVelocity;
				animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, leanPose.GetInstructions(0f, input), 20);
			}
		}

		public void Maneuver(bool maneuver)
		{
			if (maneuver != maneuvering)
			{
				maneuvering = maneuver;
				if (maneuvering)
				{
					controlMod.SetValue(0f);
				}
				else
				{
					controlMod.SetValue(1f);
					animatorPoser.RevokeInstructions(this);
				}
			}
		}
	}
}
