using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaxUtils.StateMachine;
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
		[SerializeField, Conditional(nameof(animateIK))] private int ikPriority;
		[SerializeField, Conditional(nameof(animateIK))] private bool asStateTransition;
		[SerializeField, Conditional(nameof(animateIK))] private float reachDuration = 0.5f;
		[SerializeField, Conditional(nameof(animateIK))] private float recoverDuration = 1f;

		private AgentArmsComponent arms;
		private CallbackService callbackService;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;
		private IEquipmentComponent equipment;

		private EntityStat entityTimeScale;
		private FloatOperationModifier controlMod;
		private Coroutine coroutine;

		private RuntimeEquipedData leftEquip;
		private ArmedEquipmentComponent leftComp;
		private RuntimeEquipedData rightEquip;
		private ArmedEquipmentComponent rightComp;

		public void InjectDependencies(IEntity entity, AgentArmsComponent arms, CallbackService callbackService,
			IIKComponent ik, RigidbodyWrapper rigidbodyWrapper, IEquipmentComponent equipment)
		{
			this.arms = arms;
			this.callbackService = callbackService;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.equipment = equipment;

			entityTimeScale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, false);
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

			// Subscribe to events.
			callbackService.LateUpdateCallback += OnLateUpdate;
			equipment.EquipedEvent += OnEquipedEvent;
			equipment.UnequipingEvent += OnUnquipingEvent;

			foreach (RuntimeEquipedData item in equipment.EquipedItems)
			{
				OnEquipedEvent(item);
			}

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

			// Unsubscribe from events.
			callbackService.LateUpdateCallback -= OnLateUpdate;
			equipment.EquipedEvent -= OnEquipedEvent;
			equipment.UnequipingEvent -= OnUnquipingEvent;

			// Clean up
			if (coroutine != null)
			{
				callbackService.StopCoroutine(coroutine);
				coroutine = null;
			}
			ik.RemoveInfluencer(this, IKChainConstants.LEFT_ARM);
			ik.RemoveInfluencer(this, IKChainConstants.RIGHT_ARM);
			rigidbodyWrapper.Control.RemoveModifier(this);

			leftEquip = null;
			leftComp = null;
			rightEquip = null;
			rightComp = null;

			arms.ResetArms();
		}

		private void OnLateUpdate()
		{
			if(!sheathe)
			{
				if (leftComp != null)
				{
					arms.UpdateArm(true, leftComp.ArmedSettings, Time.deltaTime);
				}
				if (rightComp != null)
				{
					arms.UpdateArm(false, rightComp.ArmedSettings, Time.deltaTime);
				}
			}
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

		private void OnEquipedEvent(RuntimeEquipedData data)
		{
			if (data.Slot.ID == HumanBoneIdentifiers.LEFT_HAND)
			{
				leftEquip = data;
				leftComp = leftEquip.EquipedInstance.GetComponent<ArmedEquipmentComponent>();
			}

			if (data.Slot.ID == HumanBoneIdentifiers.RIGHT_HAND)
			{
				rightEquip = data;
				rightComp = rightEquip.EquipedInstance.GetComponent<ArmedEquipmentComponent>();
			}
		}

		private void OnUnquipingEvent(RuntimeEquipedData data)
		{
			if (data == leftEquip)
			{
				leftEquip = null;
				leftComp = null;
			}

			if (data == rightEquip)
			{
				rightEquip = null;
				rightComp = null;
			}
		}
	}
}
