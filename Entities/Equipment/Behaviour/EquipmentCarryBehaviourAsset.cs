using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "EquipmentCarryBehaviourAsset", menuName = "ScriptableObjects/EquipmentCarryBehaviourAsset")]
	public class EquipmentCarryBehaviourAsset : BehaviourAsset
	{
		private float ElbowHintWeight
		{
			set { if (isLeft) { finalIK.LeftElbowHintWeight = value; } else { finalIK.RightElbowHintWeight = value; } }
		}

		[SerializeField] private bool debug;
		[SerializeField] private int ikPrio = 0;
		[Header("Position")]
		[SerializeField] private float accelerationInfluence = 1f;
		[SerializeField] private float maxAccelerationInfluence = 1f;
		[SerializeField] private float velocityInfluence = 1f;
		[SerializeField] private float maxVelocityInfluence = 1f;
		[Header("Rotation")]
		[SerializeField] private float smoothMaxAngle = 60f;
		[SerializeField] private float absoluteMaxAngle = 90f;
		[SerializeField] private float smoothAnglePower = 10f;

		private RuntimeEquipedData equipedData;
		private IAgent agent;
		private AgentArmsComponent arms;
		private IIKComponent ik;
		private TransformLookup lookup;
		private RigidbodyWrapper rigidbodyWrapper;
		private FinalIKComponent finalIK;
		private CallbackService callbackService;

		private bool isLeft;
		private Transform hand;
		private string ikChain;

		private Vector3 targetPosSmooth;
		private Vector3 posVelocity;
		private Quaternion targetRotationSmooth;
		private Quaternion rotVelocity;

		public void InjectDependencies(RuntimeEquipedData equipedData, IAgent agent, IIKComponent ik, AgentArmsComponent arms,
			TransformLookup lookup, RigidbodyWrapper rigidbodyWrapper, FinalIKComponent finalIK, CallbackService callbackService)
		{
			this.equipedData = equipedData;
			this.agent = agent;
			this.arms = arms;
			this.ik = ik;
			this.lookup = lookup;
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.finalIK = finalIK;
			this.callbackService = callbackService;
		}

		public override void Start()
		{
			base.Start();

			isLeft = equipedData.Slot.Type == EquipmentSlotTypes.LEFT_HAND;
			hand = isLeft ? arms.LeftHand : arms.RightHand;
			ikChain = isLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;

			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, OnUpdate);
			callbackService.DrawGizmosCallback += OnDrawGizmos;
			arms.SheathedEvent += OnSheathedEvent;
		}

		public override void Stop()
		{
			base.Stop();

			callbackService.UnsubscribeUpdates(this);
			arms.SheathedEvent -= OnSheathedEvent;
			callbackService.DrawGizmosCallback -= OnDrawGizmos;

			targetPosSmooth = Vector3.zero;
			posVelocity = Vector3.zero;
			ik.RemoveInfluencer(this, ikChain);
			ElbowHintWeight = 0f;
		}

		public void OnUpdate(float delta)
		{
			if (arms.Sheathed)
			{
				return;
			}

			// GATHER CONTROL DATA.
			(Vector3 pos, Quaternion rot) orientation = arms.GetHandSlotOrientation(isLeft, false);
			Vector3 positionOffset = hand.position - orientation.pos;
			Quaternion rotationOffset = orientation.rot.Inverse() * hand.rotation;

			float mass = equipedData.RuntimeItemData.TryGetStat(AgentStatIdentifiers.MASS, out float m) ? m : 1f;
			float strength = agent.TryGetStat(AgentStatIdentifiers.STRENGTH, out EntityStat s) ? s : 1f;
			float smoothTime = mass / (strength * 0.1f);

			// CALCULATE POSITION.
			Vector3 targetPos = hand.position - agent.Transform.position;
			//targetPos += (rigidbodyWrapper.Acceleration * accelerationInfluence).ClampMagnitude(maxAccelerationInfluence);
			//targetPos += (rigidbodyWrapper.Velocity * velocityInfluence).ClampMagnitude(maxVelocityInfluence);
			targetPosSmooth =
				targetPosSmooth == Vector3.zero ?
					targetPos :
					targetPosSmooth.SmoothDamp(targetPos, ref posVelocity, smoothTime, delta);

			// - Prevent position going out of bounds.
			targetPosSmooth = (targetPosSmooth + agent.Transform.position).LocalizePoint(agent.Transform);
			if (targetPosSmooth.z < 0f) targetPosSmooth.z = 0f;
			if (isLeft && targetPosSmooth.x > 0f || !isLeft && targetPosSmooth.x < 0f) targetPosSmooth.x = 0f;
			targetPosSmooth = targetPosSmooth.GlobalizePoint(agent.Transform) - agent.Transform.position;

			// - Prevent position passing through body.
			Vector3 flat = targetPosSmooth.FlattenY();
			if (flat.magnitude < agent.Body.Bumper.radius)
			{
				targetPosSmooth = flat.ClampMagnitude(agent.Body.Bumper.radius, float.MaxValue).SetY(targetPosSmooth.y);
			}

			// CALCULATE ROTATION.
			Quaternion targetRotation = orientation.rot;
			targetRotationSmooth =
				targetRotationSmooth == Quaternion.identity ?
					targetRotation :
					targetRotationSmooth.SmoothDamp(targetRotation, ref rotVelocity, smoothTime, delta);
			targetRotationSmooth = targetRotationSmooth.SmoothClampForward(agent.Transform.forward, smoothMaxAngle, absoluteMaxAngle, smoothAnglePower * delta);

			// APPLY INFLUENCE.
			ElbowHintWeight = 0.5f * arms.Weight;
			targetPosSmooth = targetPosSmooth.Lerp(targetPos, arms.Weight.Value.Invert());
			targetRotationSmooth = targetRotationSmooth.Slerp(targetRotation, arms.Weight.Value.Invert());
			ik.AddInfluencer(this, ikChain, ikPrio,
				agent.Transform.position + targetPosSmooth + targetRotation * positionOffset, arms.Weight,
				targetRotationSmooth * rotationOffset, arms.Weight);
		}

		private void OnSheathedEvent(bool sheathed)
		{
			// Reset variables.
			targetPosSmooth = Vector3.zero;
			posVelocity = Vector3.zero;
			ik.RemoveInfluencer(this, ikChain);
			ElbowHintWeight = 0f;
		}

		private void OnDrawGizmos()
		{
			if (!debug)
			{
				return;
			}

			if (hand == null)
			{
				hand = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND);
			}

			Gizmos.color = Color.magenta;
			Gizmos.DrawWireSphere(hand.position, 0.02f);
			Gizmos.color = Color.red;
			Gizmos.DrawLine(hand.position, agent.Transform.position + targetPosSmooth);
			Gizmos.DrawSphere(agent.Transform.position + targetPosSmooth, 0.02f);
		}
	}
}
