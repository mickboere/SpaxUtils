namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Node interface implementing <see cref="IRule"/> that should be able to return the next <see cref="FlowStateNode"/> to transition to.
	/// </summary>
	public interface ITransitionComponent : IStateComponent, IRule
	{
		/// <summary>
		/// Returns the next <see cref="FlowStateNode"/>.
		/// </summary>
		FlowStateNode GetNextState();
	}
}