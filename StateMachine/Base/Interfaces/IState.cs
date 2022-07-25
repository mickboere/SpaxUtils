using System.Collections.Generic;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// Interface for a single state within a statemachine.
	/// </summary>
	/// <seealso cref="IStateComponent"/>
	/// <seealso cref="IUnique"/>
	public interface IState : IStateComponent, IUnique
	{
		/// <summary>
		/// The name of this state.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Returns all of the components that should be activated when this state is active.
		/// This could also include parent components or child components.
		/// </summary>
		List<IStateComponent> GetAllComponents();
	}
}
