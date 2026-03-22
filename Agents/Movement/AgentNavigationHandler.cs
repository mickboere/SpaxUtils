using SpaxUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpaxUtils
{
	/// <summary>
	/// Result of a one-shot path query. Does not affect the handler's cached navigation path.
	/// </summary>
	public struct PathQueryResult
	{
		/// <summary>Whether a path was found at all (not <see cref="NavMeshPathStatus.PathInvalid"/>).</summary>
		public bool Valid;

		/// <summary>Whether the path reaches the destination exactly (<see cref="NavMeshPathStatus.PathComplete"/>).</summary>
		public bool Complete;

		/// <summary>Total path length in world units.</summary>
		public float Length;

		/// <summary>Estimated traversal time based on <see cref="Length"/> and the queried speed.</summary>
		public float EstimatedTime;
	}

	/// <summary>
	/// Agent component that handles NPC movement and navigation.
	/// </summary>
	[DefaultExecutionOrder(29)]
	public class AgentNavigationHandler : EntityComponentMono, IDependency
	{
		private const float DEFAULT_ACCURACY = 0.1f;

		/// <summary>
		/// Range used when snapping arbitrary world positions onto the NavMesh
		/// for path queries that originate from positions not guaranteed to be on the mesh.
		/// </summary>
		private const float QUERY_SAMPLE_RANGE = 5f;

		[Header("NavMesh")]
		[SerializeField, Tooltip("How far the target must drift from its last-calculated position before the path is recalculated.")]
		private float recalculationThreshold = 1f;

		[SerializeField, Tooltip("Corner advance tolerance expressed as seconds of travel time. Scales with speed so faster agents don't overshoot corners.")]
		private float cornerAdvanceTime = 0.15f;

		[Header("Steering")]
		[SerializeField, Tooltip("Layers considered solid obstacles for wall avoidance.")]
		private LayerMask steeringObstacleLayers;

		[SerializeField, Tooltip("How far ahead to cast wall detection rays.")]
		private float wallRayLength = 1f;
		[SerializeField, Tooltip("Angle in degrees between the center ray and each side ray for wall and cliff detection.")]
		private float steeringRayAngle = 35f;

		[SerializeField, Tooltip("How far ahead of the agent to probe for cliff edges.")]
		private float cliffLookAheadDist = 0.6f;

		[SerializeField, Tooltip("Downward distance at which no ground counts as a cliff.")]
		private float cliffDropThreshold = 1.5f;

		[Header("Debug")]
		[SerializeField] private bool debugGizmos;

		private IAgent agent;
		private IAgentMovementHandler movementHandler;
		private ITargetable targetable;
		private Coroutine coroutine;

		// Last steered direction and target, kept for gizmo drawing.
		private Vector3 debugSteerDir;
		private Vector3 debugMoveTarget;
		private bool debugHasTarget;
		private int debugLastSteerFrame = -1;

		private NavMeshPath navMeshPath;
		private int cornerIndex;
		private Vector3 pathTarget;
		private float cachedPathLength;
		private bool pathValid;

		public void InjectDependencies(
			Agent agent,
			IAgentMovementHandler movementHandler,
			ITargetable targetable)
		{
			this.agent = agent;
			this.movementHandler = movementHandler;
			this.targetable = targetable;
		}

		private void OnEnable()
		{
			navMeshPath = new NavMeshPath();
			agent.SubscribeOptimizedUpdate(OnOptimizedUpdate);
		}

		private void OnDisable()
		{
			agent.UnsubscribeOptimizedUpdate(OnOptimizedUpdate);
		}

		private void OnOptimizedUpdate(float delta)
		{
			if (!pathValid)
			{
				return;
			}

			Vector3 currentTarget = GetTargetPosition();

			if (Vector3.SqrMagnitude(currentTarget - pathTarget) > recalculationThreshold * recalculationThreshold)
			{
				CalculatePath(currentTarget);
			}
		}

		public void ResetInput(bool resetSmoothInput = false)
		{
			movementHandler.InputRaw = Vector3.zero;
			if (resetSmoothInput)
			{
				movementHandler.InputSmooth = Vector3.zero;
				agent.Body.RigidbodyWrapper.Velocity = Vector3.zero;
			}
		}

		#region Path Query

		/// <summary>
		/// Performs a one-shot NavMesh path query from the agent's current position to <paramref name="destination"/>.
		/// Snaps the agent position onto the NavMesh first so the query works even when
		/// the agent stands slightly above or beside the mesh surface.
		/// Does not affect the cached navigation path used by <see cref="FollowNavMeshPath"/>.
		/// </summary>
		/// <param name="destination">World-space target position (should already be on or near the NavMesh).</param>
		/// <param name="speed">Movement speed used to estimate traversal time via <see cref="IAgentMovementHandler.CalculateSpeed"/>.</param>
		public PathQueryResult QueryPath(Vector3 destination, float speed)
		{
			PathQueryResult result = new PathQueryResult();

			// Snap agent position onto the NavMesh to get a valid start point.
			Vector3 origin = agent.Transform.position;
			if (NavMesh.SamplePosition(origin, out NavMeshHit originHit, QUERY_SAMPLE_RANGE, NavMesh.AllAreas))
			{
				origin = originHit.position;
			}
			else
			{
				// Agent is nowhere near any NavMesh. Cannot query.
				return result;
			}

			NavMeshPath tempPath = new NavMeshPath();
			bool found = NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, tempPath);

			result.Valid = found && tempPath.status != NavMeshPathStatus.PathInvalid;
			result.Complete = found && tempPath.status == NavMeshPathStatus.PathComplete;

			if (result.Valid)
			{
				result.Length = CalculatePathLength(tempPath.corners, 0);
				float worldSpeed = movementHandler.CalculateSpeed(speed);
				result.EstimatedTime = worldSpeed > 0.001f ? result.Length / worldSpeed : float.MaxValue;
			}

			return result;
		}

		#endregion

		#region Targeting

		/// <summary>
		/// Has the agent navigate towards the target's position.
		/// </summary>
		/// <param name="range">The minimum range to get within.</param>
		/// <param name="speed">The speed at which to move. Default is 1.</param>
		/// <param name="navMesh">Use navmesh pathing?</param>
		/// <param name="target">Target position override.</param>
		/// <returns>Whether the agent is within range of the target.</returns>
		public bool MoveInRange(float range, float speed = 1f, bool navMesh = false, Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);

			if (debugGizmos)
			{
				debugMoveTarget = targetPosition;
				debugHasTarget = true;
			}

			if (IsInRange(range, navMesh, targetPosition))
			{
				ResetInput();
				return true;
			}

			if (navMesh)
			{
				FollowNavMeshPath(targetPosition, speed);
			}
			else
			{
				movementHandler.InputAxis = Direction(targetPosition);
				movementHandler.InputRaw = Vector3.forward * speed;
			}

			return false;
		}

		private void FollowNavMeshPath(Vector3 targetPosition, float speed)
		{
			if (!pathValid || Vector3.SqrMagnitude(targetPosition - pathTarget) > recalculationThreshold * recalculationThreshold)
			{
				CalculatePath(targetPosition);
			}

			if (!pathValid || navMeshPath.corners.Length == 0)
			{
				// Path failed entirely, fall back to direct movement.
				movementHandler.InputAxis = Direction(targetPosition);
				movementHandler.InputRaw = Vector3.forward * speed;
				return;
			}

			float tolerance = movementHandler.CalculateSpeed(speed) * cornerAdvanceTime;
			while (cornerIndex < navMeshPath.corners.Length - 1 &&
				   Vector3.Distance(agent.Transform.position, navMeshPath.corners[cornerIndex]) < tolerance)
			{
				cornerIndex++;
			}

			Vector3 cornerDirection = Direction(navMeshPath.corners[cornerIndex]);

			// InputAxis rejects zero vectors, guard before assigning.
			if (cornerDirection.sqrMagnitude < 0.001f)
			{
				ResetInput();
				return;
			}

			movementHandler.InputAxis = cornerDirection;
			movementHandler.InputRaw = Vector3.forward * speed;
		}

		/// <summary>
		/// Will rotate the agent towards the target.
		/// </summary>
		public void RotateTowardsTarget(Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);
			movementHandler.TargetDirection = Direction(targetPosition);
		}

		/// <summary>
		/// Returns the closest <see cref="IEntity"/>.
		/// </summary>
		public bool TryGetClosestEntity(IEnumerable<IEntity> entities, bool navMesh, out IEntity closest, out float distance)
		{
			closest = null;
			distance = float.MaxValue;

			foreach (IEntity entity in entities)
			{
				float d = Distance(navMesh, entity.GameObject.transform.position);
				if (d < distance)
				{
					closest = entity;
					distance = d;
				}
			}

			return closest != null;
		}

		/// <summary>
		/// Returns the closest <see cref="ITargetable"/>.
		/// </summary>
		public bool TryGetClosestTarget(IEnumerable<ITargetable> targetables, bool navmesh, out ITargetable closest, out float closestDistance)
		{
			closest = null;
			closestDistance = float.MaxValue;

			foreach (ITargetable targetable in targetables)
			{
				if (!targetable.IsTargetable)
				{
					continue;
				}

				float distance = Distance(agent.GameObject.transform.position, targetable.Position, navmesh);
				if (distance < closestDistance)
				{
					closest = targetable;
					closestDistance = distance;
				}
			}

			return closest != null;
		}

		/// <summary>
		/// Returns whether the target lies within <paramref name="range"/>.
		/// </summary>
		public bool IsInRange(float range, bool navMesh, Vector3? target = null)
		{
			return range > Distance(navMesh, target);
		}

		public float Distance(Vector3 from, Vector3 to, bool navMesh = false)
		{
			if (navMesh)
			{
				return NavMeshPathLength(from, to);
			}

			return Direction(from, to).magnitude;
		}

		public float Distance(bool navMesh = false, Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);

			if (navMesh)
			{
				if (pathValid && Vector3.SqrMagnitude(targetPosition - pathTarget) <= recalculationThreshold * recalculationThreshold)
				{
					return RemainingPathLength();
				}

				CalculatePath(targetPosition);
				return pathValid ? cachedPathLength : Distance(agent.Transform.position, targetPosition, false);
			}

			return Distance(agent.Transform.position, targetPosition, false);
		}

		public Vector3 Direction(Vector3 from, Vector3 to)
		{
			return to - from;
		}

		public Vector3 Direction(Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);
			return Direction(agent.GameObject.transform.position, targetPosition);
		}

		public Vector3 GetTargetPosition(Vector3? target = null)
		{
			return target.HasValue ? target.Value :
				agent.Targeter.Target != null ? agent.Targeter.Target.Position :
				agent.GameObject.transform.position;
		}

		#endregion

		#region Aligning

		public bool IsAlignedApprox(Vector3 position, Vector3 direction, float accuracy = DEFAULT_ACCURACY)
		{
			return Distance(agent.Transform.position, position) < accuracy &&
				agent.Transform.forward.Dot(direction) > 1f - accuracy;
		}

		public void ForceAlign(Vector3 position, Vector3 direction)
		{
			agent.Transform.position = position;
			agent.Transform.forward = direction;
			movementHandler.TargetDirection = direction;
			ResetInput(true);
		}

		public void AlignWithPoint(Vector3 position, Vector3 direction, float speed = 1f, bool navMesh = false, Action callback = null)
		{
			StopEnumerator();

			if (IsAlignedApprox(position, direction))
			{
				ForceAlign(position, direction);
				callback?.Invoke();
			}
			else
			{
				StartEnumerator(AlignEnumerator());
			}

			IEnumerator AlignEnumerator()
			{
				while (!MoveInRange(DEFAULT_ACCURACY, speed, navMesh, position))
				{
					yield return null;
				}
				ForceAlign(position, direction);
				callback?.Invoke();
			}
		}

		#endregion

		#region Steering

		/// <summary>
		/// Steers the agent using a world-space walk direction.
		/// Sets InputAxis to the (possibly deflected) walk direction and InputRaw to Vector3.forward * walkDirection.magnitude.
		/// If a lookDirection is provided it overrides the facing direction, otherwise the agent faces the walk direction.
		/// Returns false if movement was obstructed. If applyAvoidance is true, direction is deflected away from walls and
		/// cliff edges where possible; all-three-rays cliff is always a hard stop regardless.
		/// <paramref name="hardStop"/> is true when there is no safe direction to deflect toward.
		/// </summary>
		public bool TrySteerWorld(Vector3 walkDirection, out bool hardStop, Vector3? lookDirection = null, bool applyAvoidance = true)
		{
			hardStop = false;

			if (walkDirection.sqrMagnitude < 0.001f)
			{
				ResetInput();
				return true;
			}

			float magnitude = walkDirection.magnitude;
			Vector3 flatWalkDir = walkDirection.FlattenY().normalized;

			if (debugGizmos)
			{
				debugMoveTarget = agent.Transform.position + flatWalkDir * magnitude;
				debugHasTarget = true;
			}

			bool clear = ApplySteering(ref flatWalkDir, out hardStop, applyAvoidance);

			if (hardStop)
			{
				ResetInput();
				return false;
			}

			movementHandler.InputAxis = lookDirection.HasValue ? lookDirection.Value : flatWalkDir;
			movementHandler.InputRaw = Vector3.forward * magnitude;
			return clear;
		}

		/// <summary>
		/// Steers the agent using a local-space input and a world-space look direction.
		/// Matches the pattern used by strafing behaviours: InputAxis = lookDirection, InputRaw = localInput.
		/// Returns false if movement was obstructed. If applyAvoidance is true, direction is deflected away from walls and
		/// cliff edges where possible; all-three-rays cliff is always a hard stop regardless.
		/// <paramref name="hardStop"/> is true when there is no safe direction to deflect toward.
		/// </summary>
		public bool TrySteerLocal(Vector3 localInput, Vector3 lookDirection, out bool hardStop, bool applyAvoidance = true)
		{
			hardStop = false;

			if (localInput.sqrMagnitude < 0.001f)
			{
				ResetInput();
				return true;
			}

			// Convert local input to world-space walk direction for safety checks.
			Vector3 worldWalkDir = (Quaternion.LookRotation(lookDirection.FlattenY().normalized) * localInput).FlattenY().normalized;

			bool clear = ApplySteering(ref worldWalkDir, out hardStop, applyAvoidance);

			if (hardStop)
			{
				ResetInput();
				return false;
			}

			// If avoidance deflected the direction, transform back to local space and preserve magnitude.
			if (!clear && applyAvoidance)
			{
				Vector3 deflectedLocal = Quaternion.Inverse(Quaternion.LookRotation(lookDirection.FlattenY().normalized)) * worldWalkDir;
				deflectedLocal = deflectedLocal.normalized * localInput.magnitude;
				movementHandler.InputAxis = lookDirection;
				movementHandler.InputRaw = deflectedLocal;
			}
			else
			{
				movementHandler.InputAxis = lookDirection;
				movementHandler.InputRaw = localInput;
			}

			return clear;
		}

		/// <summary>
		/// Runs wall and cliff safety checks against the given world-space flat direction.
		/// Modifies the direction in-place when applyAvoidance is true.
		/// Returns true if the path is clear.
		/// <paramref name="hardStop"/> is true only when all three cliff rays detect a drop - no safe direction exists.
		/// </summary>
		private bool ApplySteering(ref Vector3 flatWorldDir, out bool hardStop, bool applyAvoidance)
		{
			hardStop = false;

			if (debugGizmos)
			{
				debugSteerDir = flatWorldDir;
				debugLastSteerFrame = Time.frameCount;
			}

			// --- Cliff check (three-ray fan) ---
			// Rays originate from targetable.Center projected forward and cast downward.
			// Three rays in a fan to catch diagonal approaches.
			// The elevated origin (center height) clears slopes and stairs before casting down.
			Vector3 centerHeight = targetable.Center;
			Vector3 leftCliffDir = Quaternion.AngleAxis(-steeringRayAngle, Vector3.up) * flatWorldDir;
			Vector3 rightCliffDir = Quaternion.AngleAxis(steeringRayAngle, Vector3.up) * flatWorldDir;

			Vector3 cliffOriginC = centerHeight + flatWorldDir * cliffLookAheadDist;
			Vector3 cliffOriginL = centerHeight + leftCliffDir * cliffLookAheadDist;
			Vector3 cliffOriginR = centerHeight + rightCliffDir * cliffLookAheadDist;

			// Total downward cast covers center height above feet plus the cliff drop threshold.
			float cliffCastDist = (centerHeight.y - agent.Transform.position.y) + cliffDropThreshold;

			bool cliffC = !Physics.Raycast(cliffOriginC, Vector3.down, cliffCastDist, steeringObstacleLayers);
			bool cliffL = !Physics.Raycast(cliffOriginL, Vector3.down, cliffCastDist, steeringObstacleLayers);
			bool cliffR = !Physics.Raycast(cliffOriginR, Vector3.down, cliffCastDist, steeringObstacleLayers);

			if (cliffC || cliffL || cliffR)
			{
				if (cliffL && cliffR)
				{
					// All directions lead off the edge, hard stop regardless of applyAvoidance.
					hardStop = true;
					return false;
				}

				if (applyAvoidance)
				{
					// Deflect away from the cliff edge toward the safe side.
					// If only one side is clear, rotate fully toward it.
					// If center is the only cliff, both sides are safe; pick the one with more forward progress.
					if (!cliffL && cliffR)
					{
						// Right side is cliff, steer left.
						flatWorldDir = leftCliffDir;
					}
					else if (cliffL && !cliffR)
					{
						// Left side is cliff, steer right.
						flatWorldDir = rightCliffDir;
					}
					else
					{
						// Only center ray is cliff; pick whichever side keeps more forward progress.
						flatWorldDir = Vector3.Dot(leftCliffDir, flatWorldDir) >= Vector3.Dot(rightCliffDir, flatWorldDir)
							? leftCliffDir
							: rightCliffDir;
					}
				}
				else
				{
					hardStop = true;
				}

				return false;
			}

			// --- Wall check (three-point raycast) ---
			Vector3 rayOrigin = targetable.Center;
			Vector3 leftDir = Quaternion.AngleAxis(-steeringRayAngle, Vector3.up) * flatWorldDir;
			Vector3 rightDir = Quaternion.AngleAxis(steeringRayAngle, Vector3.up) * flatWorldDir;

			bool centerHit = Physics.Raycast(rayOrigin, flatWorldDir, out RaycastHit centerHitInfo, wallRayLength, steeringObstacleLayers);
			bool leftHit = Physics.Raycast(rayOrigin, leftDir, out RaycastHit leftHitInfo, wallRayLength, steeringObstacleLayers);
			bool rightHit = Physics.Raycast(rayOrigin, rightDir, out RaycastHit rightHitInfo, wallRayLength, steeringObstacleLayers);

			if (!centerHit && !leftHit && !rightHit)
			{
				return true;
			}

			if (!applyAvoidance)
			{
				return false;
			}

			// Choose which wall normal to slide along.
			// Prefer the side ray that gives the largest dot product with original direction (most forward progress).
			Vector3 deflected;

			if (centerHit)
			{
				// Use the center hit normal as primary deflection surface.
				deflected = Vector3.ProjectOnPlane(flatWorldDir, centerHitInfo.normal).FlattenY().normalized;
			}
			else
			{
				// Only side rays hit; pick the deflection that keeps more forward progress.
				Vector3 leftDeflect = leftHit ? Vector3.ProjectOnPlane(flatWorldDir, leftHitInfo.normal).FlattenY().normalized : flatWorldDir;
				Vector3 rightDeflect = rightHit ? Vector3.ProjectOnPlane(flatWorldDir, rightHitInfo.normal).FlattenY().normalized : flatWorldDir;

				deflected = Vector3.Dot(leftDeflect, flatWorldDir) >= Vector3.Dot(rightDeflect, flatWorldDir)
					? leftDeflect
					: rightDeflect;
			}

			if (deflected.sqrMagnitude > 0.001f)
			{
				flatWorldDir = deflected;
			}

			return false;
		}

		#endregion

		#region NavMesh

		private void CalculatePath(Vector3 targetPosition)
		{
			navMeshPath.ClearCorners();
			cornerIndex = 0;
			pathTarget = targetPosition;

			bool found = NavMesh.CalculatePath(
				agent.Transform.position,
				targetPosition,
				NavMesh.AllAreas,
				navMeshPath);

			// Partial paths are followed to their endpoint by design.
			pathValid = found && navMeshPath.status != NavMeshPathStatus.PathInvalid;

			if (pathValid)
			{
				cachedPathLength = CalculatePathLength(navMeshPath.corners, 0);

				// NavMesh sometimes places the first corner at the agent's feet; skip it if so.
				if (navMeshPath.corners.Length > 1 &&
					Vector3.Distance(agent.Transform.position, navMeshPath.corners[0]) <
					cornerAdvanceTime * movementHandler.CalculateSpeed(1f))
				{
					cornerIndex = 1;
				}
			}
			else
			{
				cachedPathLength = 0f;
			}
		}

		private float CalculatePathLength(Vector3[] corners, int fromIndex)
		{
			float length = 0f;
			for (int i = fromIndex; i < corners.Length - 1; i++)
			{
				length += Vector3.Distance(corners[i], corners[i + 1]);
			}
			return length;
		}

		private float RemainingPathLength()
		{
			if (!pathValid || navMeshPath.corners.Length == 0)
			{
				return 0f;
			}

			float remaining = Vector3.Distance(agent.Transform.position, navMeshPath.corners[cornerIndex]);
			remaining += CalculatePathLength(navMeshPath.corners, cornerIndex);
			return remaining;
		}

		/// <summary>
		/// One-shot path length between two arbitrary world points. Does not affect the cached path.
		/// </summary>
		private float NavMeshPathLength(Vector3 from, Vector3 to)
		{
			NavMeshPath tempPath = new NavMeshPath();
			bool found = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, tempPath);

			if (!found || tempPath.status == NavMeshPathStatus.PathInvalid)
			{
				return Vector3.Distance(from, to);
			}

			return CalculatePathLength(tempPath.corners, 0);
		}

		#endregion

		#region Coroutines

		private void StartEnumerator(IEnumerator enumerator)
		{
			StopEnumerator();
			coroutine = StartCoroutine(enumerator);
		}

		private void StopEnumerator()
		{
			if (coroutine != null)
			{
				StopCoroutine(coroutine);
				coroutine = null;
			}
		}

		#endregion

		#region Debug Gizmos

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (!debugGizmos || agent == null)
			{
				return;
			}

			Vector3 agentPos = agent.Transform.position;

			// Draw current movement target.
			if (debugHasTarget)
			{
				Gizmos.color = Color.yellow;
				Gizmos.DrawSphere(debugMoveTarget, 0.12f);

				if (pathValid && navMeshPath != null && navMeshPath.corners.Length > 1)
				{
					// Draw NavMesh path corners and connecting lines.
					Gizmos.color = Color.cyan;
					Gizmos.DrawLine(agentPos, navMeshPath.corners[0]);

					for (int i = 0; i < navMeshPath.corners.Length; i++)
					{
						Gizmos.DrawSphere(navMeshPath.corners[i], 0.06f);

						if (i < navMeshPath.corners.Length - 1)
						{
							// Highlight the active corner in a brighter color.
							Gizmos.color = i == cornerIndex ? Color.white : Color.cyan;
							Gizmos.DrawLine(navMeshPath.corners[i], navMeshPath.corners[i + 1]);
							Gizmos.color = Color.cyan;
						}
					}
				}
				else
				{
					// No path, draw direct line to target.
					Gizmos.color = new Color(1f, 0.5f, 0f);
					Gizmos.DrawLine(agentPos, debugMoveTarget);
				}
			}

			// Draw steering rays only when steering was actually performed this frame.
			if (targetable != null && debugLastSteerFrame == Time.frameCount)
			{
				Vector3 steerOrigin = targetable.Center;
				Vector3 steerDir = debugSteerDir.sqrMagnitude > 0.001f
					? debugSteerDir
					: agent.Transform.forward;

				Vector3 leftCliffDir = Quaternion.AngleAxis(-steeringRayAngle, Vector3.up) * steerDir;
				Vector3 rightCliffDir = Quaternion.AngleAxis(steeringRayAngle, Vector3.up) * steerDir;
				float cliffCastDist = (steerOrigin.y - agentPos.y) + cliffDropThreshold;

				// Cliff rays - red if no ground detected, green if ground found.
				DrawSteeringRay(steerOrigin + steerDir * cliffLookAheadDist, Vector3.down, cliffCastDist);
				DrawSteeringRay(steerOrigin + leftCliffDir * cliffLookAheadDist, Vector3.down, cliffCastDist);
				DrawSteeringRay(steerOrigin + rightCliffDir * cliffLookAheadDist, Vector3.down, cliffCastDist);

				// Wall rays - orange if hit, green if clear.
				DrawSteeringRay(steerOrigin, steerDir, wallRayLength);
				DrawSteeringRay(steerOrigin, leftCliffDir, wallRayLength);
				DrawSteeringRay(steerOrigin, rightCliffDir, wallRayLength);
			}
		}

		private void DrawSteeringRay(Vector3 origin, Vector3 direction, float length)
		{
			bool hit = Physics.Raycast(origin, direction, out RaycastHit hitInfo, length, steeringObstacleLayers);
			Gizmos.color = hit ? new Color(1f, 0.4f, 0f) : Color.green;
			Gizmos.DrawRay(origin, direction * (hit ? hitInfo.distance : length));

			if (hit)
			{
				Gizmos.DrawSphere(hitInfo.point, 0.04f);
			}
		}
#endif

		#endregion
	}
}
