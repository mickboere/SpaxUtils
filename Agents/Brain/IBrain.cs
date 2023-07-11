using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for a state machine that is in a single <see cref="string"/> state at any time.
	/// </summary>
	public interface IBrain : IDisposable
	{
		/// <summary>
		/// The lower-most state the brain currently finds itself in.
		/// </summary>
		string CurrentState { get; }

		/// <summary>
		/// All active states leading up to the current state.
		/// </summary>
		IReadOnlyList<string> StateHierarchy { get; }

		/// <summary>
		/// Have the brain enter the state named <paramref name="state"/>.
		/// </summary>
		/// <param name="state">The name of the state to enter.</param>
		void EnterState(string state);

		/// <summary>
		/// Returns whether <paramref name="state"/> can be found in the current state hierarchy.
		/// </summary>
		/// <param name="state">The name of the state to check the hierarchy for.</param>
		/// <returns>Whether <paramref name="state"/> can be found in the current state hierarchy.</returns>
		bool IsInState(string state);

		/// <summary>
		/// Adds the <paramref name="component"/> to the <paramref name="state"/> to receive <see cref="IStateComponent"/> callbacks when the state is active.
		/// </summary>
		/// <param name="state">The name of the state to add the component to.</param>
		/// <param name="component">The component to add.</param>
		void AddComponent(string state, IStateComponent component);

		/// <summary>
		/// Removes the <paramref name="component"/> from <paramref name="state"/> to stop it from receiving <see cref="IStateComponent"/> callbacks.
		/// </summary>
		/// <param name="state">The name of the state to remove the component from.</param>
		/// <param name="component">The component to remove.</param>
		void RemoveComponent(string state, IStateComponent component);

		/// <summary>
		/// Will append the <paramref name="graph"/> data to this brain, copying its components and adding them to the appropriate <see cref="BrainState"/>s.
		/// </summary>
		/// <param name="graph">The <see cref="StateMachineGraph"/> to append the data of.</param>
		void AddGraph(StateMachineGraph graph);

		/// <summary>
		/// Will remove the appended data of <paramref name="graph"/> from this brain.
		/// </summary>
		/// <param name="graph">The <see cref="StateMachineGraph"/> to remove the data of.</param>
		void RemoveGraph(StateMachineGraph graph);
	}
}
