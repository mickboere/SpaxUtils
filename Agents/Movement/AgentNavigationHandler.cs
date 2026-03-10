using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that handles NPC movement and navigation.
	/// </summary>
	[DefaultExecutionOrder(29)]
	public class AgentNavigationHandler : EntityComponentMono, IDependency
	{
		private const float DEFAULT_ACCURACY = 0.1f;

		[Header("NavMesh")]
		[SerializeField, Tooltip("How far the target must drift from its last-calculated position before the path is recalculated.")]
		private float recalculationThreshold = 1f;

		[SerializeField, Tooltip("Corner advance tolerance expressed as seconds of travel time. Scales with speed so faster agents don't overshoot corners.")]
		private float cornerAdvanceTime = 0.5f;

		private IAgent agent;
		private IAgentMovementHandler movementHandler;
		private Coroutine coroutine;

		private NavMeshPath navMeshPath;
		private int cornerIndex;
		private Vector3 pathTarget;
		private float cachedPathLength;
		private bool pathValid;

		public void InjectDependencies(
			Agent agent,
			IAgentMovementHandler movementHandler)
		{
			this.agent = agent;
			this.movementHandler = movementHandler;
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
				return;

			Vector3 currentTarget = GetTargetPosition();

			if (Vector3.SqrMagnitude(currentTarget - pathTarget) > recalculationThreshold * recalculationThreshold)
				CalculatePath(currentTarget);
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
				CalculatePath(targetPosition);

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
					continue;

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
				return NavMeshPathLength(from, to);

			return Direction(from, to).magnitude;
		}

		public float Distance(bool navMesh = false, Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);

			if (navMesh)
			{
				if (pathValid && Vector3.SqrMagnitude(targetPosition - pathTarget) <= recalculationThreshold * recalculationThreshold)
					return RemainingPathLength();

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
				length += Vector3.Distance(corners[i], corners[i + 1]);
			return length;
		}

		private float RemainingPathLength()
		{
			if (!pathValid || navMeshPath.corners.Length == 0)
				return 0f;

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
				return Vector3.Distance(from, to);

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
	}
}
