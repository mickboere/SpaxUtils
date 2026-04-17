namespace SpaxUtils
{
	/// <summary>
	/// Base class for components attached to an <see cref="IAgent"/>. Implements <see cref="EntityComponentMono"/>.
	/// </summary>
	public abstract class AgentComponentBase : EntityComponentMono
	{
		public IAgent Agent
		{
			get
			{
				if (_agent == null)
				{
					_agent = gameObject.GetComponentInParent<IAgent>();
				}

				return _agent;
			}
		}
		private IAgent _agent;

		public virtual void InjectDependencies(IAgent agent)
		{
			_agent = agent;
		}
	}
}
