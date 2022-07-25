using System;
using System.Collections.Generic;

namespace SpaxUtils.StateMachine
{
	/// <summary>
	/// State component interface, contains several callbacks relating to the current parent state.
	/// </summary>
	public interface IStateComponent
	{
		/// <summary>
		/// The order of execution. Lower means earlier execution.
		/// </summary>
		int ExecutionOrder { get; }

		/// <summary>
		/// Defines whether this component should have its state dependencies injected after <see cref="OnPrepare"/>.
		/// </summary>
		bool InjectStateDependencies { get; }

		/// <summary>
		/// Called before InjectDependencies().
		/// <see cref="IStateComponent"/>s are not guaranteed to be deleted after use, use this method to reinitialize if necessary.
		/// </summary>
		void OnPrepare();

		/// <summary>
		/// Called after preparing the state, delays the <see cref="OnStateEntered"/> until all <paramref name="onCompleteCallback"/>s have returned.
		/// </summary>
		/// <param name="onCompleteCallback">The callback that needs to be called as soon as the entry transition has completed.</param>
		void OnEnteringState(Action onCompleteCallback);

		/// <summary>
		/// Called when the state containing this component is entered.
		/// </summary>
		void OnStateEntered();

		/// <summary>
		/// Called as a state is being exited, delays the <see cref="OnStateExit"/> as well as next state until all <paramref name="onCompleteCallback"/>s have returned.
		/// </summary>
		/// <param name="onCompleteCallback">The callback that needs to be called as soon as the exit transition has completed.</param>
		void OnExitingState(Action onCompleteCallback);

		/// <summary>
		/// Called when the state containing this node has exited.
		/// </summary>
		void OnStateExit();

		/// <summary>
		/// Called every update frame while the state is active.
		/// </summary>
		void OnUpdate();

		/// <summary>
		/// Returns all of the components connected directly to this component.
		/// </summary>
		List<IStateComponent> GetComponents();
	}
}
