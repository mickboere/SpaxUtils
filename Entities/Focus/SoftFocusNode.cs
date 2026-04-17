using UnityEngine;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Passive brain node that registers a soft entity focus provider with the <see cref="FocusHandler"/>.
	/// Evaluates all visible agents each tick via <see cref="VisionComponent"/>,
	/// prioritising enemies over any other visible agent.
	/// </summary>
	public class SoftFocusNode : StateComponentNodeBase
	{
		private IAgent agent;
		private FocusHandler focusHandler;
		private IVisionComponent vision;
		private ITargeter targeter;
		private IEntityCollection entityCollection;

		private EntityComponentFilter<ITargetable> allAgentTargetables;
		private System.Func<Vector3?> provider;

		public void InjectDependencies(
			IAgent agent,
			FocusHandler focusHandler,
			IVisionComponent vision,
			ITargeter targeter,
			IEntityCollection entityCollection)
		{
			this.agent = agent;
			this.focusHandler = focusHandler;
			this.vision = vision;
			this.targeter = targeter;
			this.entityCollection = entityCollection;
		}

		public override void OnStateEntered()
		{
			base.OnStateEntered();

			if (allAgentTargetables == null)
			{
				// Track all ITargetables excluding self.
				allAgentTargetables = new EntityComponentFilter<ITargetable>(
					entityCollection,
					agent);
			}

			provider = GetSoftFocusPoint;
			focusHandler.Register(FocusHandler.PRIORITY_SOFT_ENTITY, provider);
		}

		public override void OnStateExit()
		{
			base.OnStateExit();

			focusHandler.Unregister(provider);
			allAgentTargetables?.Dispose();
			allAgentTargetables = null;
		}

		private Vector3? GetSoftFocusPoint()
		{
			// Enemies get priority within the visible set.
			ITargetable best = vision.GetMostLikelyTarget(targeter.Enemies.Components);
			if (best != null)
			{
				return best.Point;
			}

			// Then any other visible agent.
			best = vision.GetMostLikelyTarget(allAgentTargetables.Components);
			if (best != null)
			{
				return best.Point;
			}

			return null;
		}
	}
}
