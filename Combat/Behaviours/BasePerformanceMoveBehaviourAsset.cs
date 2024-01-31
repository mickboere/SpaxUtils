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
		[SerializeField] private float controlWeightSmoothing = 6f;
		[SerializeField] private bool blockArms;

		protected IMovePerformer performer;
		protected RigidbodyWrapper rigidbodyWrapper;
		protected AgentArmsComponent arms;

		private FloatOperationModifier controlMod;
		private float weight;

		public void InjectDependencies(IMovePerformer performer,
			RigidbodyWrapper rigidbodyWrapper, AgentArmsComponent arms)
		{
			this.performer = performer;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.arms = arms;
		}

		public override void Start()
		{
			base.Start();

			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);

			if (blockArms)
			{
				arms.Weight.AddModifier(this, controlMod);
			}

			// Retrieve pose updates to set control weight.
			performer.PoseUpdateEvent += OnPoseUpdateEvent;
		}

		public override void Stop()
		{
			base.Stop();

			rigidbodyWrapper.Control.RemoveModifier(this);

			if (blockArms)
			{
				arms.Weight.RemoveModifier(this);
			}

			performer.PoseUpdateEvent -= OnPoseUpdateEvent;

			controlMod.Dispose();
		}

		public virtual void ExUpdate(float delta)
		{
			// Set control from pose weight.
			float control = 1f - weight;
			controlMod.SetValue(controlMod.Value < control ? Mathf.Lerp(controlMod.Value, control, controlWeightSmoothing * Time.deltaTime) : control);
		}

		private void OnPoseUpdateEvent(IPerformer performer, PoserStruct pose, float weight)
		{
			this.weight = weight;
		}
	}
}
