using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class AgentStunHandlerComponent : EntityComponentBase, IStunHandler
	{
		public bool Stunned { get; private set; }

		#region Tooltips
		private const string TT_RECOVERY_THRESH = "Upper velocity threshold below which Agent begins to recover.";
		private const string TT_RECOVERED_THRESH = "Lower velocity threshold below which control is fully returned to Agent.";
		#endregion Tooltips

		[Header("Grounded")]
		[SerializeField] private PoseSequenceBlendTree groundedHitBlendTree;
		[SerializeField] private float minStunTime = 0.5f;
		[SerializeField, Tooltip(TT_RECOVERY_THRESH)] private float recoveryThreshold = 2f;
		[SerializeField, Tooltip(TT_RECOVERED_THRESH)] private float recoveredThreshold = 1f;
		[Header("Airborne")]
		[SerializeField] private PoseSequence airborneHitPose;
		[SerializeField] private float airborneThreshold = 15f;

		private bool airborne;
		private HitData stunHit;
		private FloatOperationModifier controlMod;
		private FloatOperationModifier armsMod;
		private TimerStruct stunTimer;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private IAgentMovementHandler movementHandler;
		private AgentArmsComponent arms;

		public void InjectDependencies(IAgent agent, RigidbodyWrapper rigidbodyWrapper,
			AnimatorPoser animatorPoser, IAgentMovementHandler movementHandler,
			AgentArmsComponent arms)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.movementHandler = movementHandler;
			this.arms = arms;
		}

		protected void OnEnable()
		{
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
			armsMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			arms.Weight.AddModifier(this, armsMod);
		}

		protected void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
			arms.Weight.RemoveModifier(this);
		}

		protected void Update()
		{
			if (Stunned)
			{
				if (airborne)
				{
					float stunAmount = Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed);

					movementHandler.ForceRotation(-rigidbodyWrapper.Velocity);

					animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, airborneHitPose.Evaluate(0f), 10, stunAmount);
					controlMod.SetValue(0f);
					armsMod.SetValue(stunAmount.Invert());
					agent.Actor.Blocked = true;

					if (rigidbodyWrapper.Speed < recoveredThreshold)
					{
						Stunned = false;
					}
				}
				else
				{
					float stunAmount = Mathf.Max(stunTimer.Progress.Invert(), Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed));

					PoserStruct instructions = groundedHitBlendTree.GetInstructions(-stunHit.Direction.Localize(rigidbodyWrapper.transform), 0f);
					animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunAmount);
					controlMod.SetValue(0f);
					armsMod.SetValue(stunAmount.Invert());
					agent.Actor.Blocked = true;

					if (stunTimer.Expired && rigidbodyWrapper.Speed < recoveredThreshold)
					{
						Stunned = false;
					}
				}
			}
			else
			{
				animatorPoser.RevokeInstructions(this);
				controlMod.SetValue(1f);
				armsMod.SetValue(1f);
				agent.Actor.Blocked = false;
			}
		}

		public void EnterStun(HitData stunHit)
		{
			Stunned = true;
			this.stunHit = stunHit;
			stunTimer = new TimerStruct(minStunTime);
			airborne = rigidbodyWrapper.Speed > airborneThreshold;

			//SpaxDebug.Log("Stun", $"spd={rigidbodyWrapper.Speed}, airborne={airborne}", color: Color.green);
		}
	}

	public interface IStunHandler
	{
		bool Stunned { get; }

		void EnterStun(HitData hitData);
	}
}
