namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IBehaviour"/>s that can be run by an <see cref="IAgent.Mind"/> to take control of the agent's functions./
	/// </summary>
	public interface IMindBehaviour : IBehaviour
	{
		bool Interuptable { get; }

		bool Valid(Vector8 motivation, IEntity target, out float distance);
	}
}
