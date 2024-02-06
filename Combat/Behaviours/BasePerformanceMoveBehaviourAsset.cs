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

		protected PerformanceState State => Performer.State;
		protected float Weight { get; private set; }

		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField] private bool blockArms;

		private FloatOperationModifier controlMod;
		
		public void InjectDependencies(IAgent agent,
			IMovePerformer performer, IPerformanceMove move,
			RigidbodyWrapper rigidbodyWrapper, AgentArmsComponent arms)
		{
			Agent = agent;
			Performer = performer;
			Move = move;
			RigidbodyWrapper = rigidbodyWrapper;
			Arms = arms;
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

			// Retrieve pose updates to set control weight.
			Performer.PoseUpdateEvent += OnPoseUpdateEvent;
		}

		public override void Stop()
		{
			base.Stop();

			RigidbodyWrapper.Control.RemoveModifier(this);

			if (blockArms)
			{
				Arms.Weight.RemoveModifier(this);
			}

			Performer.PoseUpdateEvent -= OnPoseUpdateEvent;

			controlMod.Dispose();
		}

		public virtual void CustomUpdate(float delta)
		{
			// Set control from pose weight.
			float control = 1f - Weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			this.Weight = weight;
		}
	}
}
