using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Base (abstract) implementation for <see cref="IPerformanceMove.Behaviour"/> assets.
	/// Ties movement control to pose weight.
	/// </summary>
	public abstract class BasePerformanceMoveBehaviourAsset : BehaviourAsset, IUpdatable
	{
		protected IAgent Agent { get; private set; }
		protected IMovePerformer Performer { get; private set; }
		protected IPerformanceMove Move { get; private set; }
		protected RigidbodyWrapper RigidbodyWrapper { get; private set; }
		protected AgentArmsComponent Arms { get; private set; }
		protected AnimatorPoser Poser { get; private set; }

		protected PerformanceState State => Performer.State;
		protected IPoserInstructions PoserInstructions { get; private set; }
		protected float Weight { get; private set; }

		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField] private bool blockArms;

		private FloatOperationModifier controlMod;

		public void InjectDependencies(IAgent agent,
			IMovePerformer performer, IPerformanceMove move,
			RigidbodyWrapper rigidbodyWrapper, AgentArmsComponent arms,
			AnimatorPoser poser)
		{
			Agent = agent;
			Performer = performer;
			Move = move;
			RigidbodyWrapper = rigidbodyWrapper;
			Arms = arms;
			Poser = poser;
		}

		public override void Start()
		{
			base.Start();

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			RigidbodyWrapper.Control.AddModifier(this, controlMod);

			if (blockArms)
			{
				Arms.Weight.AddModifier(this, controlMod);
			}
		}

		public override void Stop()
		{
			base.Stop();

			RigidbodyWrapper.Control.RemoveModifier(this);

			if (blockArms)
			{
				Arms.Weight.RemoveModifier(this);
			}

			Poser.RevokeInstructions(this);
		}

		public virtual void CustomUpdate(float delta)
		{
			PoserInstructions = Evaluate(out float weight);
			Weight = weight;

			Poser.ProvideInstructions(this, PoserLayerConstants.BODY, PoserInstructions, 2, Weight);

			// Set control from pose weight.
			float control = 1f - Weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * delta) : control);
		}

		protected abstract IPoserInstructions Evaluate(out float weight);
	}
}
