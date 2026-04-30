namespace SpaxUtils
{
	/// <summary>
	/// Interface for <see cref="IBehaviour"/>s that can be run by an <see cref="IAgent.Mind"/> to take control of the agent's functions./
	/// </summary>
	public interface IMindBehaviour : IBehaviour
	{
		/// <summary>
		/// The user-facing name for this behaviour.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Valid higher priority behaviours are always chosen over lower priority behaviours.
		/// </summary>
		int Priority { get; }

		/// <summary>
		/// If a behaviour is not interuptable (FALSE) it cannot be stopped and no other behaviours can be started until it is set to interuptable (TRUE).
		/// </summary>
		bool Interuptable { get; }

		///// <summary>
		///// The minimum required motivation for this behaviour to be triggered / considered valid.
		///// </summary>
		//Vector8 Trigger { get; }

		/// <summary>
		/// Evaluates whether this behaviour can run against <paramref name="candidate"/> and returns the chosen target and strength.
		/// Simple behaviours validate the candidate directly. Compound behaviours may override to select a different target.
		/// </summary>
		/// <param name="candidate">The entity with the highest-magnitude stimuli, as chosen by the mind.</param>
		/// <param name="candidateStimuli">The signed stimuli vector for <paramref name="candidate"/>.</param>
		(IEntity target, float strength) Evaluate(IEntity candidate, Vector8 candidateStimuli);

		/// <summary>
		/// Returns whether this behaviour is valid to be initiated against <paramref name="target"/>.
		/// </summary>
		/// <param name="stimuli">The signed stimuli vector for <paramref name="target"/>.</param>
		/// <param name="target">The entity being evaluated.</param>
		/// <param name="strength">Strength of the match; higher means a better fit.</param>
		bool Valid(Vector8 stimuli, IEntity target, out float strength);
	}
}
