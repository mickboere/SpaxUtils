using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachine;
using System;

namespace SpaxUtils
{
	public class SheatheArmsNode : StateMachineNodeBase
	{
		[SerializeField, Input(backingValue = ShowBackingValue.Never)] protected Connections.StateComponent inConnection;

		[SerializeField] private bool sheathe;
		[SerializeField] private bool animateIK;
		[SerializeField, Conditional(nameof(animateIK))] private int ikPriority;
		[SerializeField, Conditional(nameof(animateIK))] private bool asStateTransition;
		[SerializeField, Conditional(nameof(animateIK))] private float reachDuration = 0.5f;
		[SerializeField, Conditional(nameof(animateIK))] private float recoverDuration = 1f;

		private IEntity entity;
		private AgentArmsComponent arms;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;

		private EntityStat entityTimeScale;
		private FloatOperationModifier controlMod;
		private Coroutine coroutine;

		public void InjectDependencies(IEntity entity, AgentArmsComponent arms, CallbackService callbackService, IIKComponent ik, RigidbodyWrapper rigidbodyWrapper)
		{
			this.entity = entity;
			this.arms = arms;
			this.callbackService = callbackService;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;

			entityTimeScale = entity.GetStat(EntityStatIdentifier.TIMESCALE, false);
			controlMod?.Dispose();
			controlMod = new FloatOperationModifier(ModMethod.Absolute, Operation.Multiply, 1f);
			rigidbodyWrapper.Control.AddModifier(this, controlMod);
		}

		public override void OnEnteringState(Action callback)
		{
			if (animateIK && asStateTransition && sheathe != arms.Sheathed)
			{
				// Move hand IK target towards sheathe.
				coroutine = callbackService.StartCoroutine(Animate(reachDuration, false, callback));
			}
			else
			{
				callback();
			}
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (sheathe != arms.Sheathed)
			{
				if (asStateTransition)
				{
					Recover();
				}
				else
				{
					// Move hand IK target towards sheathe, use Recover() as callback.
					coroutine = callbackService.StartCoroutine(Animate(reachDuration, false, Recover));
				}
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
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}

			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);
			rigidbodyWrapper.Control.RemoveModifier(this);
		}

		private IEnumerator Animate(float duration, bool invert, Action callback)
		{
			Timer t = new Timer(duration * (1f / (entityTimeScale ?? 1f)));
			while (t)
			{
				float p = invert ? t.Progress.ReverseInOutCubic() : t.Progress.InOutCubic();
				controlMod.SetValue(p.Invert());
				if (arms.LeftVisual != null)
				{
					var orientation = GetSheathingOrientation(true);
					ik.AddInfluencer(this, IKChainConstants.LEFT_ARM, ikPriority, orientation.pos, p, orientation.rot, p);
				}
				if (arms.RightVisual != null)
				{
					var orientation = GetSheathingOrientation(false);
					ik.AddInfluencer(this, IKChainConstants.RIGHT_ARM, ikPriority, orientation.pos, p, orientation.rot, p);
				}
				yield return null;
			}

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
