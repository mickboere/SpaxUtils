using SpaxUtils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component that handles NPC movement and navigation.
	/// </summary>
	public class AgentNavigationHandler : EntityComponentBase, IDependency
	{
		// TODO: Figure out per-entity what distance type to use (suspended = true, grounded = false)
		// OR: Make it non-binary, meaning Y is flattened more as the height-difference increases.

		private IAgent agent;
		private IAgentMovementHandler agentMovementHandler;

		public void InjectDependencies(
			Agent agentEntity,
			IAgentMovementHandler agentMovementHandler)
		{
			this.agent = agentEntity;
			this.agentMovementHandler = agentMovementHandler;
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
				agentMovementHandler.SetMovementInput(Vector2.zero);
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
				agentMovementHandler.SetInputAxis(direction);
				// Order NPC to move forwards towards target direction.
				agentMovementHandler.SetMovementInput(Vector2.up * speed);
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
			agentMovementHandler.SetTargetDirection(Direction(targetPosition));
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
		public bool TryGetClosestTarget(IEnumerable<ITargetable> targetables, out ITargetable closest, out float distance)
		{
			closest = null;
			distance = float.MaxValue;

			foreach (ITargetable targetable in targetables)
			{
				if (!targetable.IsTargetable)
				{
					continue;
				}

				// Utilize Look-direction. For player: camera direction. For NPC: facing direction.

				float d = Distance(false, targetable.Position);
				if (d < distance)
				{
					closest = targetable;
					distance = d;
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
			return target ?? agent.Targeter.Target.Position;
		}
	}
}
