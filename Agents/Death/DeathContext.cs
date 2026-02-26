namespace SpaxUtils
{
	public struct DeathContext
	{
		public IAgent Died { get; }
		public IEntity Source { get; }
		string Cause { get; }

		public DeathContext(IAgent died, IEntity source, string cause = "")
		{
			Died = died;
			Source = source;
			Cause = cause;
		}
	}
}
