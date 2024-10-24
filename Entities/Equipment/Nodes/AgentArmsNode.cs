using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachines;
using System;

namespace SpaxUtils
{
	/// <summary>
	/// Agent node that either sheathes or keeps up arms, updating their IK to match equiped items.
	/// </summary>
	public class AgentArmsNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		[SerializeField] private bool sheathe;
		[SerializeField] private bool animateIK;
		[SerializeField] private bool animateControl;
		[SerializeField, Conditional(nameof(animateIK))] private int ikPriority = 2;
		[SerializeField, Conditional(nameof(animateIK))] private float reachDuration = 0.5f;
		[SerializeField, Conditional(nameof(animateIK))] private float recoverDuration = 1f;

		private AgentArmsComponent arms;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;

		private EntityStat entityTimeScale;
		private FloatOperationModifier controlMod;
		private Coroutine coroutine;

		public void InjectDependencies(IEntity entity, AgentArmsComponent arms, CallbackService callbackService,
			IIKComponent ik, RigidbodyWrapper rigidbodyWrapper)
		{
			this.arms = arms;
			this.callbackService = callbackService;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;

			entityTimeScale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, false);
		}

		public override void OnEnteringState()
		{
			if (animateControl)
			{
				controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
				rigidbodyWrapper.Control.AddModifier(this, controlMod);
			}
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (sheathe != arms.Sheathed)
			{
				// Move hand IK target towards sheathe, use Recover() as callback.
				coroutine = callbackService.StartCoroutine(Animate(reachDuration, false, Recover));
			}

			void Recover()
			{
				// Apply parenting.
				arms.SheatheArms(sheathe);

				// Recover IK from sheathing point.
				coroutine = callbackService.StartCoroutine(Animate(recoverDuration, true, () => coroutine = null));
			}
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			// Clean up
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}
			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);

			if (animateControl)
			{
				rigidbodyWrapper.Control.RemoveModifier(this);
				controlMod.Dispose();
			}
		}

		private IEnumerator Animate(float duration, bool invert, Action callback)
		{
			TimerStruct t = new TimerStruct(duration * (1f / (entityTimeScale ?? 1f)));
			while (t)
			{
				float weight = invert ? t.Progress.ReverseInOutCubic() : t.Progress.InOutCubic();
				controlMod?.SetValue(weight.Invert());
				if (arms.LeftVisual != null)
				{
					var orientation = GetSheathingOrientation(true);
					ik.AddInfluencer(this, IKChainConstants.LEFT_ARM, ikPriority, orientation.pos, weight, orientation.rot, weight);
				}
				if (arms.RightVisual != null)
				{
					var orientation = GetSheathingOrientation(false);
					ik.AddInfluencer(this, IKChainConstants.RIGHT_ARM, ikPriority, orientation.pos, weight, orientation.rot, weight);
				}
				yield return null;
			}

			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);

			callback?.Invoke();
		}

		private (Vector3 pos, Quaternion rot) GetSheathingOrientation(bool left)
		{
			(Vector3 pos, Quaternion rot) orientation = arms.GetHandSlotOrientation(left, true);

			Transform hand = left ? arms.LeftHand : arms.RightHand;
			orientation.pos = orientation.pos * hand.lossyScale.x;

			Transform sheathe = left ? arms.LeftSheathe : arms.RightSheathe;
			orientation.rot = sheathe.rotation * orientation.rot;
			orientation.pos = sheathe.position - orientation.rot * orientation.pos;

			Debug.DrawRay(orientation.pos, orientation.rot * Vector3.right * 0.2f, Color.red);
			Debug.DrawRay(orientation.pos, orientation.rot * Vector3.up * 0.2f, Color.green);
			Debug.DrawRay(orientation.pos, orientation.rot * Vector3.forward * 0.2f, Color.blue);

			return orientation;
		}
	}
}
