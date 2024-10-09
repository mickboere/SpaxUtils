using SpaxUtils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that handles NPC movement and navigation.
	/// </summary>
	[DefaultExecutionOrder(29)]
	public class AgentNavigationHandler : EntityComponentBase, IDependency
	{
		private IAgent agent;
		private IAgentMovementHandler movementHandler;

		public void InjectDependencies(
			Agent agent,
			IAgentMovementHandler movementHandler)
		{
			this.agent = agent;
			this.movementHandler = movementHandler;
		}

		/// <summary>
		/// Has the agent navigate towards the target's position.
		/// </summary>
		/// <param name="range">The minimum range to get within with.</param>
		/// <param name="speed">The speed at which to move at. Default is 1.</param>
		/// <param name="navMesh">Use navmesh?</param>
		/// <param name="target">Target position override.</param>
		/// <returns>Whether the agent is within range of the target.</returns>
		public bool MoveInRange(float range, float speed = 1f, bool navMesh = false, Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);

			if (IsInRange(range, navMesh, targetPosition))
			{
				movementHandler.InputRaw = Vector3.zero;
				return true;
			}
			else if (navMesh)
			{
				Debug.LogError("NavMesh is not implemented yet.");
			}
			else
			{
				// Get direction to target position.
				Vector3 direction = Direction(targetPosition);
				// Set direction as target movement direction.
				movementHandler.InputAxis = direction;
				// Order NPC to move forwards towards target direction.
				movementHandler.InputRaw = Vector3.forward * speed;
			}

			return false;
		}

		/// <summary>
		/// Will rotate the agent towards the target.
		/// </summary>
		/// <param name="target"></param>
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
				Debug.LogWarning("NavMesh is not implemented yet.");
				return 0f;
			}
			else
			{
				return Direction(from, to).magnitude;
			}
		}

		public float Distance(bool navMesh = false, Vector3? target = null)
		{
			Vector3 targetPosition = GetTargetPosition(target);
			return Distance(agent.GameObject.transform.position, targetPosition, navMesh);
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
	}
}
