using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "CombatBehaviour_Ranged", menuName = "ScriptableObjects/Combat/RangedCombatBehaviourAsset")]
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

		public override void Start()
		{
			base.Start();
			Performer.PerformanceStartedEvent += OnPerformanceStartedEvent;
		}

		public override void Stop()
		{
			base.Stop();
			Performer.PerformanceStartedEvent -= OnPerformanceStartedEvent;
		}

		private void OnPerformanceStartedEvent(IPerformer performer)
		{
			// First frame of performance.
			instanceTimer = new TimerClass(move.InstanceDelay, () => timescaleStat, callbackService);
		}

		public override void ExternalUpdate(float delta)
		{
			base.ExternalUpdate(delta);

			if (instanceTimer != null && instanceTimer.Expired)
			{
				// Instantiate projectile.
				Vector3 location = Agent.Transform.position;
				if (!string.IsNullOrEmpty(move.InstanceLocation))
				{
					location = transformLookup.Lookup(move.InstanceLocation).position;
				}
				GameObject instance = DependencyUtils.InstantiateAndInject(move.ProjectilePrefab, location, Agent.Transform.forward.LookRotation(), dependencyManager, true, false);
				if (instance.TryGetComponent(out IProjectile projectile))
				{
					projectile.Range = move.Range;
				}
				instanceTimer.Dispose();
				instanceTimer = null;
			}
		}
	}
}
