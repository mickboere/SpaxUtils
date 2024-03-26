using System;
using System.Collections.Generic;
using SpaxUtils.StateMachines;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for a single-headed dynamic state machine with total control over an <see cref="IAgent"/>.
	/// </summary>
	public interface IBrain : IDisposable
	{
		/// <summary>
		/// All active states leading up to the <see cref="HeadState"/>.
		/// </summary>
		IReadOnlyList<IState> StateHierarchy { get; }

		/// <summary>
		/// The currently active head-state.
		/// </summary>
		IState HeadState { get; }

		/// <summary>
		/// Adds a new <see cref="BrainState"/> to the brain.
		/// </summary>
		/// <param name="state">The state to add to the brain.</param>
		void AddState(BrainState state);

		/// <summary>
		/// Returns whether the brain currently contains data for a state named <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The state ID to check for.</param>
		/// <returns>Whether the brain currently contains data for a state named <paramref name="id"/>.</returns>
		bool HasState(string id);

		/// <summary>
		/// Returns whether the brain currently contains data for a state named <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The state ID to check for.</param>
		/// <returns>Whether the brain currently contains data for a state named <paramref name="id"/>.</returns>
		bool TryGetState(string id, out BrainState state);

		/// <summary>
		/// Ensure a brainstate named <paramref name="id"/> exists and return it.
		/// </summary>
		/// <param name="id">The name of the state to ensure.</param>
		/// <param name="template">If the requested state is null, use this state as a template for the new state.</param>
		/// <returns>A <see cref="BrainState"/> with ID <paramref name="id"/> and optionally <paramref name="template"/>'s hierarchy (but not components).</returns>
		BrainState EnsureState(string id, IState template = null);

		/// <summary>
		/// Returns whether <paramref name="id"/> can be found in the current state hierarchy.
		/// </summary>
		/// <param name="id">The name of the state to check the hierarchy for.</param>
		/// <returns>Whether <paramref name="id"/> can be found in the current state hierarchy.</returns>
		bool IsStateActive(string id);

		/// <summary>
		/// Have the brain enter a state with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the state to transition to.</param>
		/// <param name="transition">The intermediary transition object to use.</param>
		/// <returns>Whether the transition was successfully initiated.</returns>
		bool TryTransition(string id, ITransition transition);

		/// <summary>
		/// Adds the <paramref name="component"/> to the <paramref name="id"/> to receive <see cref="IStateComponent"/> callbacks when the state is active.
		/// </summary>
		/// <param name="id">The name of the state to add the component to.</param>
		/// <param name="component">The component to add.</param>
		bool TryAddComponent(string id, IStateComponent component);

		/// <summary>
		/// Removes the <paramref name="component"/> from <paramref name="id"/> to stop it from receiving <see cref="IStateComponent"/> callbacks.
		/// </summary>
		/// <param name="id">The name of the state to remove the component from.</param>
		/// <param name="component">The component to remove.</param>
		bool TryRemoveComponent(string id, IStateComponent component);

		/// <summary>
		/// Will append the <paramref name="graph"/> data to this brain, copying its components and adding them to the appropriate <see cref="BrainState"/>s.
		/// </summary>
		/// <param name="graph">The <see cref="StateMachineGraph"/> to append the data of.</param>
		void AppendGraph(StateMachineGraph graph);

		/// <summary>
		/// Will remove the appended data of <paramref name="graph"/> from this brain.
		/// </summary>
		/// <param name="graph">The <see cref="StateMachineGraph"/> to remove the data of.</param>
		void StripGraph(StateMachineGraph graph);
	}
}
