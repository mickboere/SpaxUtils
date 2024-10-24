namespace SpaxUtils
{
	/// <summary>
	/// Base class for components attached to an <see cref="IAgent"/>. Implements <see cref="EntityComponentBase"/>.
	/// </summary>
	public abstract class AgentComponentBase : EntityComponentBase
	{
		public IAgent Agent
		{
			get
			{
				if (agent == null)
				{
					agent = gameObject.GetComponentInParent<IAgent>();
				}

				return agent;
			}
		}

		private IAgent agent;

		public virtual void InjectDependencies(IAgent agent)
		{
			this.agent = agent;
		}
	}
}
