using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "RangedCombatBehaviourAsset", menuName = "ScriptableObjects/Combat/RangedCombatBehaviourAsset")]
	public class RangedCombatBehaviourAsset : BaseCombatMoveBehaviourAsset
	{
		private IRangedCombatMove move;
		private IDependencyManager dependencyManager;
		private CallbackService callbackService;
		private TransformLookup transformLookup;

		private EntityStat timescaleStat;

		private TimerClass instanceTimer;

		public void InjectDependencies(IRangedCombatMove move, IDependencyManager dependencyManager,
			CallbackService callbackService, TransformLookup transformLookup)
		{
			this.move = move;
			this.dependencyManager = dependencyManager;
			this.callbackService = callbackService;
			this.transformLookup = transformLookup;

			timescaleStat = Agent.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
		}

		public override void CustomUpdate(float delta)
		{
			base.CustomUpdate(delta);

			if (Performer.State == PerformanceState.Performing && Performer.RunTime.Approx(0f))
			{
				// First frame of performance.
				instanceTimer = new TimerClass(move.InstanceDelay, () => timescaleStat, callbackService);
			}

			if (instanceTimer != null && instanceTimer.Expired)
			{
				// Instantiate projectile.
				Vector3 location = Agent.Transform.position;
				if (!string.IsNullOrEmpty(move.InstanceLocation))
				{
					location = transformLookup.Lookup(move.InstanceLocation).position;
				}
				DependencyUtils.InstantiateAndInject(move.ProjectilePrefab, location, Agent.Transform.forward.LookRotation(), dependencyManager, true, false);
				instanceTimer.Dispose();
				instanceTimer = null;
			}
		}
	}
}
