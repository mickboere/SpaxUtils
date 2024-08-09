namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IBehaviour"/>s that can be run by an <see cref="IAgent.Mind"/> to take control of the agent's functions./
	/// </summary>
	public interface IMindBehaviour : IBehaviour
	{
		/// <summary>
		/// Valid higher priority behaviours are always chosen over lower priority behaviours.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// If a behaviour is not interuptable (FALSE) it cannot be stopped and no other behaviours can be started until it is set to interuptable (TRUE).
		/// </summary>
		bool Interuptable { get; }

		/// <summary>
		/// The minimum required motivation for this behaviour to be triggered / considered valid.
		/// </summary>
		//Vector8 Trigger { get; }

		/// <summary>
		/// Returns whether this behaviour is valid to be intiated.
		/// </summary>
		/// <param name="motivation">The agent's current motivation profile, used to determine whether the minimum motivation is met for behaviour initiation.</param>
		/// <param name="target">The <see cref="IEntity"/> this motivation is currently targeting.</param>
		/// <param name="distance">The current distance from the motivation being met. When multiple behaviours of the same priority are valid, the one with the lowest distance will be chosen.</param>
		/// <returns>Whether this behaviour is valid to be initiated.</returns>
		bool Valid(Vector8 motivation, IEntity target, out float distance);
	}
}
