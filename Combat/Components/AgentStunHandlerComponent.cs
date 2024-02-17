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
		[SerializeField] private PoseSequenceBlendTree airborneHitBlendTree;
		[SerializeField] private float airborneThreshold = 15f;

		private bool airborne;
		private HitData stunHit;
		private FloatOperationModifier stunControlMod;
		private TimerStruct stunTimer;

		private IAgent agent;
		private RigidbodyWrapper rigidbodyWrapper;
		private AnimatorPoser animatorPoser;
		private CombatSettings combatSettings;
		private CallbackService callbackService;

		public void InjectDependencies(IAgent agent,
			RigidbodyWrapper rigidbodyWrapper, AnimatorPoser animatorPoser,
			CombatSettings combatSettings, CallbackService callbackService)
		{
			this.agent = agent;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.animatorPoser = animatorPoser;
			this.combatSettings = combatSettings;
			this.callbackService = callbackService;
		}

		protected void OnEnable()
		{
			stunControlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, stunControlMod);
		}

		protected void OnDisable()
		{
			rigidbodyWrapper.Control.RemoveModifier(this);
		}

		protected void Update()
		{
			if (Stunned)
			{
				if (airborne)
				{

				}
				else
				{
					float stunAmount = Mathf.Max(stunTimer.Progress.Invert(), Mathf.InverseLerp(recoveredThreshold, recoveryThreshold, rigidbodyWrapper.Speed));

					PoserStruct instructions = groundedHitBlendTree.GetInstructions(-stunHit.Direction.Localize(rigidbodyWrapper.transform), 0f);
					animatorPoser.ProvideInstructions(this, PoserLayerConstants.BODY, instructions, 10, stunAmount);
					stunControlMod.SetValue(0f);
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
				stunControlMod.SetValue(1f);
				agent.Actor.Blocked = false;
			}
		}

		public void EnterStun(HitData stunHit)
		{
			Stunned = true;
			this.stunHit = stunHit;
			stunTimer = new TimerStruct(minStunTime);
		}
	}

	public interface IStunHandler
	{
		bool Stunned { get; }

		void EnterStun(HitData hitData);
	}
}
