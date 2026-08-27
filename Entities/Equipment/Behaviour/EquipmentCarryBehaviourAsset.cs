using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "EquipmentCarryBehaviourAsset", menuName = "ScriptableObjects/Behaviours/EquipmentCarryBehaviourAsset")]
	public class EquipmentCarryBehaviourAsset : BehaviourAsset
	{
		/// <summary>
		/// Constrains the elbow without moving the goal: blending rest→rest leaves the authored hint be.
		/// </summary>
		private void ApplyElbowHint(float weight)
		{
			if (ik.TryGetHintRest(ikChain, out Vector3 rest))
			{
				ik.AddHintInfluencer(this, ikChain, ikPrio, rest, weight);
			}
		}

		[SerializeField, Tooltip("Draws the animated grip, its leash and where the carry has dragged it to.")]
		private bool debug;
		[SerializeField, Tooltip("IK priority of the carry. Outranked by the arms component's draw/sheathe legs.")]
		private int ikPrio = 0;
		[SerializeField, Tooltip("Seconds of lag at a load of 1, where the armament's mass equals the body's lifting strength.")]
		private float smoothTime = 0.5f;
		[Header("Lag")]
		[SerializeField, Tooltip("Scales the lag for movement only. Below 1 the grip keeps up better than it turns.")]
		private float posTimeMult = 1f;
		[SerializeField, Tooltip("Scales the lag for turning only. Below 1 the grip turns better than it keeps up.")]
		private float rotTimeMult = 1f;
		[SerializeField, Tooltip("How far the grip may trail the animation, as a fraction of arm length. Bounds the drag so a fast fall can't leave the hands behind.")]
		private float maxLagFraction = 0.5f;
		[SerializeField, Tooltip("How far the grip may trail the animation in degrees. The rotational half of the leash.")]
		private float maxLagAngle = 45f;
		[SerializeField, Tooltip("How sharply the arm stiffens as the lag extends. Higher makes it firm up sooner, so drag settles well short of the leash.")]
		private float stiffenPower = 2f;
		[Header("Aim correction")]
		[SerializeField, Tooltip("Degrees the weapon may point away from the body's forward before the carry starts correcting it.")]
		private float smoothMaxAngle = 60f;
		[SerializeField, Tooltip("Degrees the weapon's aim asymptotes towards. It eases in from the angle above and never quite reaches this one.")]
		private float absoluteMaxAngle = 90f;
		[SerializeField, Range(0f, 1f), Tooltip("How much of the aim correction to apply. 0 follows the authored poses exactly, however crooked they aim.")]
		private float forwardCorrection = 1f;

		private RuntimeEquipedData equipedData;
		private IAgent agent;
		private AgentArmsComponent arms;
		private IIKComponent ik;
		private TransformLookup lookup;
		private CallbackService callbackService;
		private EntityStat timescale;

		private bool initialized;
		private bool isLeft;
		private Transform hand;
		private string ikChain;

		private Vector3 gripPos;
		private Vector3 posVelocity;
		private Quaternion gripRot;
		private Quaternion rotVelocity;
		private bool smoothInitialized; // false until pos/rot have been snapped to target on the first update.

		private Vector3 debugTarget;
		private float debugMaxLag;

		public void InjectDependencies(RuntimeEquipedData equipedData, IAgent agent,
			AgentArmsComponent arms, TransformLookup lookup, CallbackService callbackService,
			[Optional] IIKComponent ik)
		{
			this.equipedData = equipedData;
			this.agent = agent;
			this.arms = arms;
			this.ik = ik;
			this.lookup = lookup;
			this.callbackService = callbackService;

			initialized = ik != null;

			timescale = agent.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
		}

		public override void Start()
		{
			base.Start();

			if (!initialized)
			{
				return;
			}

			isLeft = equipedData.Slot.Type == EquipmentSlotTypes.LEFT_HAND;
			hand = isLeft ? arms.LeftHand : arms.RightHand;
			ikChain = isLeft ? IKChainConstants.LEFT_ARM : IKChainConstants.RIGHT_ARM;
			smoothInitialized = false;

			callbackService.SubscribeUpdate(UpdateMode.LateUpdate, this, OnUpdate);
			callbackService.DrawGizmosCallback += OnDrawGizmos;
			arms.SheathedEvent += OnSheathedEvent;
		}

		public override void Stop()
		{
			base.Stop();

			if (!initialized)
			{
				return;
			}

			callbackService.UnsubscribeUpdates(this);
			arms.SheathedEvent -= OnSheathedEvent;
			callbackService.DrawGizmosCallback -= OnDrawGizmos;

			Snap(Vector3.zero, Quaternion.identity);
			ik.RemoveInfluencer(this, ikChain);
			ik.RemoveHintInfluencer(this, ikChain);
		}

		public void OnUpdate(float delta)
		{
			if (arms.Sheathed)
			{
				return;
			}

			delta *= timescale;

			// GATHER: the animated grip, and the hand expressed relative to it.
			(Vector3 pos, Quaternion rot) orientation = arms.GetHandSlotOrientation(isLeft, false, AgentSheatheComponent.WieldRadiusOf(equipedData));
			Vector3 handOffset = orientation.rot.Inverse() * (hand.position - orientation.pos);
			Quaternion rotationOffset = orientation.rot.Inverse() * hand.rotation;

			// CORRECT THE TARGET: the poses aren't perfect, so aim the weapon somewhere sensible.
			Vector3 targetPos = orientation.pos;
			Quaternion targetRot = CorrectAim(orientation.rot);

			// FOLLOW: heaviness is lag, and the arm stiffens as that lag extends.
			float mass = equipedData.RuntimeItemData.Mass;
			float strength = agent.Stats.TryGetStat(AgentStatIdentifiers.STRENGTH, out EntityStat s) ? s : 1f;
			float lagTime = mass / Mathf.Max(strength, 0.01f) * smoothTime;
			float maxLag = arms.ArmLength(isLeft) * maxLagFraction;

			if (!smoothInitialized || delta <= 0f || lagTime < delta)
			{
				Snap(targetPos, targetRot);
			}
			else
			{
				Follow(targetPos, targetRot, lagTime, maxLag, delta);
				Leash(targetPos, targetRot, maxLag);
				Unbury(targetPos);
			}

			smoothInitialized = true;

			// APPLY: the follower carries the grip, the hand hangs off it.
			ApplyElbowHint(0.5f * arms.Weight);
			float animated = arms.Weight.Value.Invert();
			Vector3 outPos = gripPos.Lerp(targetPos, animated);
			Quaternion outRot = gripRot.Slerp(targetRot, animated);
			ik.AddInfluencer(this, ikChain, ikPrio,
				outPos + outRot * handOffset, arms.Weight,
				outRot * rotationOffset, arms.Weight);

			debugTarget = targetPos;
			debugMaxLag = maxLag;
		}

		/// <summary>
		/// Aims the weapon back towards the body's forward once it strays, easing in over the angle band.
		/// Only the aim axis is touched; the roll stays as the animation authored it.
		/// </summary>
		private Quaternion CorrectAim(Quaternion rotation)
		{
			Vector3 aim = rotation * Vector3.forward;
			Vector3 forward = agent.Transform.forward;
			float angle = Vector3.Angle(aim, forward);
			float band = absoluteMaxAngle - smoothMaxAngle;
			if (forwardCorrection <= 0f || band <= 0f || angle <= smoothMaxAngle)
			{
				return rotation;
			}

			// Soft shoulder: free up to smoothMaxAngle, asymptoting to absoluteMaxAngle beyond it.
			float allowed = smoothMaxAngle + band * (1f - Mathf.Exp(-(angle - smoothMaxAngle) / band));
			float correction = (angle - Mathf.Lerp(angle, allowed, forwardCorrection)) * Mathf.Deg2Rad;
			return Quaternion.FromToRotation(aim, Vector3.RotateTowards(aim, forward, correction, 0f)) * rotation;
		}

		/// <summary>
		/// Chases the target with a real velocity, stiffening as the lag extends so it never snaps taut.
		/// </summary>
		private void Follow(Vector3 targetPos, Quaternion targetRot, float lagTime, float maxLag, float delta)
		{
			float posTime = Stiffened(lagTime * posTimeMult, Vector3.Distance(gripPos, targetPos), maxLag, delta);
			gripPos = gripPos.SmoothDamp(targetPos, ref posVelocity, posTime, delta);

			float rotTime = Stiffened(lagTime * rotTimeMult, Quaternion.Angle(gripRot, targetRot), maxLagAngle, delta);
			gripRot = gripRot.SmoothDamp(targetRot, ref rotVelocity, rotTime, delta);
		}

		/// <summary>Lag time shrinks as the extension approaches its limit, so the lag asymptotes short of it.</summary>
		private float Stiffened(float time, float lag, float max, float delta)
		{
			float extension = max <= 0f ? 1f : Mathf.Clamp01(lag / max);
			return Mathf.Max(time * Mathf.Pow(1f - extension, stiffenPower), delta);
		}

		/// <summary>
		/// Backstop for what stiffening can't catch (teleports, timescale spikes): clip to the leash and
		/// drop the velocity that pushed into it, so nothing can wind up against the limit.
		/// </summary>
		private void Leash(Vector3 targetPos, Quaternion targetRot, float maxLag)
		{
			Vector3 lag = gripPos - targetPos;
			float distance = lag.magnitude;
			if (maxLag > 0f && distance > maxLag)
			{
				Vector3 direction = lag / distance;
				gripPos = targetPos + direction * maxLag;
				posVelocity = distance > maxLag * 2f ?
					Vector3.zero :
					posVelocity - direction * Mathf.Max(0f, Vector3.Dot(posVelocity, direction));
			}

			if (Quaternion.Angle(gripRot, targetRot) > maxLagAngle)
			{
				gripRot = Quaternion.RotateTowards(targetRot, gripRot, maxLagAngle);
				rotVelocity = default;
			}
		}

		/// <summary>
		/// Keeps the grip out of the body while it drags. The animation defines what clear means for this
		/// pose, so the lag can never sit deeper than the pose it trails.
		/// </summary>
		private void Unbury(Vector3 targetPos)
		{
			Vector3 origin = agent.Transform.position;
			Vector3 local = gripPos - origin;
			Vector3 flat = local.FlattenY();
			float radius = Mathf.Min(agent.Body.Bumper.radius, (targetPos - origin).FlattenY().magnitude);
			if (radius <= 0f || flat.magnitude >= radius)
			{
				return;
			}

			Vector3 normal = flat.sqrMagnitude < 0.0001f ?
				agent.Transform.right * (isLeft ? -1f : 1f) :
				flat.normalized;
			gripPos = origin + normal * radius + Vector3.up * local.y;
			posVelocity -= normal * Mathf.Min(0f, Vector3.Dot(posVelocity, normal));
		}

		private void Snap(Vector3 position, Quaternion rotation)
		{
			gripPos = position;
			posVelocity = Vector3.zero;
			gripRot = rotation;
			rotVelocity = default;
		}

		private void OnSheathedEvent(bool sheathed)
		{
			// Reset variables.
			Snap(Vector3.zero, Quaternion.identity);
			smoothInitialized = false;
			ik.RemoveInfluencer(this, ikChain);
			ik.RemoveHintInfluencer(this, ikChain);
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
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(debugTarget, debugMaxLag);
			Gizmos.color = Color.red;
			Gizmos.DrawLine(debugTarget, gripPos);
			Gizmos.DrawSphere(gripPos, 0.02f);
		}
	}
}
