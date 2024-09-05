namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class for all <see cref="IPerformanceMove.Behaviour"/> assets.
	/// </summary>
	public abstract class BasePerformanceMoveBehaviourAsset : BehaviourAsset
	{
		protected IAgent Agent { get; private set; }
		protected IMovePerformer Performer { get; private set; }
		protected IPerformanceMove Move { get; private set; }

		protected PerformanceState State => Performer.State;

		public void InjectDependencies(IAgent agent, IMovePerformer performer, IPerformanceMove move)
		{
			Agent = agent;
			Performer = performer;
			Move = move;
		}
	}
}
