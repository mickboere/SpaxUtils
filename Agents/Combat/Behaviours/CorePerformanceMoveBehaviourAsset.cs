using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for CORE <see cref="IPerformanceMove.Behaviour"/> assets, meaning assets that fully control the behaviour of a perfomance move.
	/// Implements <see cref="IPrerequisite"/> with configurable grounding and sliding requirements.
	/// Subclasses can override <see cref="IsMet"/> to add additional checks (call base first).
	/// </summary>
	public abstract class CorePerformanceMoveBehaviourAsset : BasePerformanceMoveBehaviourAsset, IUpdatable, IPrerequisite
	{
		protected const string PARAM_MOVE_INDEX = "MoveIndex";
		protected const string PARAM_PREPARE = "Prepare";
		protected const string PARAM_PREPARE_TIME = "PrepareTime";
		protected const string PARAM_PERFORM = "Perform";
		protected const string PARAM_PERFORM_TIME = "PerformTime";

		protected RigidbodyWrapper RigidbodyWrapper { get; private set; }
		protected AgentArmsComponent Arms { get; private set; }
		protected AnimatorPoser Poser { get; private set; }
		protected AnimatorWrapper AnimatorWrapper { get; private set; }
		protected IPoserInstructions PoserInstructions { get; private set; }
		protected float Weight { get; private set; }

		[Header("Prerequisites")]
		[SerializeField, Tooltip("Requires the agent to be grounded to perform this move.")]
		private bool requireGrounded = true;
		[SerializeField, Conditional(nameof(requireGrounded)), Tooltip("Whether this move can be performed while sliding. Only relevant when requireGrounded is true.")]
		private bool allowSliding = false;

		[Header("Control")]
		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField] private bool blockArms;

		private FloatOperationModifier controlMod;

		public virtual bool IsMet(IDependencyManager dependencies)
		{
			if (requireGrounded)
			{
				if (!dependencies.TryGet(out GrounderComponent grounder))
				{
					return false;
				}

				if (!grounder.Grounded)
				{
					return false;
				}

				if (!allowSliding && grounder.Sliding)
				{
					return false;
				}
			}

			return true;
		}

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, AnimatorWrapper animatorWrapper,
			[Optional] AgentArmsComponent arms, [Optional] AnimatorPoser poser)
		{
			RigidbodyWrapper = rigidbodyWrapper;
			Arms = arms;
			Poser = poser;
			AnimatorWrapper = animatorWrapper;
		}

		public override void Start()
		{
			base.Start();
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			RigidbodyWrapper.Control.AddModifier(this, controlMod);
			if (Arms != null && blockArms)
			{
				Arms.Weight.AddModifier(this, controlMod);
			}
		}

		public override void Stop()
		{
			base.Stop();
			RigidbodyWrapper.Control.RemoveModifier(this);
			if (Arms != null && blockArms)
			{
				Arms.Weight.RemoveModifier(this);
			}
			if (Poser != null)
			{
				Poser.RevokeInstructions(this);
			}
		}

		public virtual void ExternalUpdate(float delta)
		{
			switch (Move.AnimationType)
			{
				case PerformanceAnimationType.Animator:
					Weight = Performer.Weight;
					HandleAnimation();
					break;
				case PerformanceAnimationType.Poser:
					PoserInstructions = Evaluate(out float weight);
					Weight = weight;
					Poser?.ProvideInstructions(this, PoserLayerConstants.BODY, PoserInstructions, 10, Weight);
					break;
			}
			// Set control from pose weight.
			float control = 1f - Weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * delta) : control);
		}

		protected virtual void HandleAnimation()
		{
			if (State is PerformanceState.Preparing)
			{
				AnimatorWrapper.SetInteger(PARAM_MOVE_INDEX, Move.AnimationIndex);
			}
			AnimatorWrapper.SetBool(PARAM_PREPARE, State == PerformanceState.Preparing);
			AnimatorWrapper.SetBool(PARAM_PERFORM, State == PerformanceState.Performing);
			float prepareTime = Move.MinCharge > 0f ? Performer.ChargeTime / Move.MinCharge : 0f;
			AnimatorWrapper.SetFloat(PARAM_PREPARE_TIME, prepareTime);
			float performTime = Move.MinDuration > 0f ? Performer.RunTime / Move.MinDuration : 0f;
			AnimatorWrapper.SetFloat(PARAM_PERFORM_TIME, performTime);
		}

		protected abstract IPoserInstructions Evaluate(out float weight);
	}
}
