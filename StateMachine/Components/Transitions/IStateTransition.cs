namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// <see cref="ITransition"/> also implementing <see cref="IRule"/>.
	/// Can be added to the <see cref="StateMachine"/> to automatically begin its transition once <see cref="IRule.Valid"/> is true.
	/// </summary>
	public interface IStateTransition : ITransition, IRule
	{
		/// <summary>
		/// Returns the ID of the state to transition to.
		/// </summary>
		string NextState { get; }
	}
}
