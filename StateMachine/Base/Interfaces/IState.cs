using System.Collections.Generic;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Interface for a single state within a state machine.
	/// </summary>
	public interface IState : IStateComponent, IIdentifiable
	{
		bool Active { get; }

		/// <summary>
		/// The parent state of this state.
		/// </summary>
		IState Parent { get; }

		/// <summary>
		/// The default child state to transition to when entering this state.
		/// </summary>
		IState DefaultChild { get; }

		/// <summary>
		/// All direct child states of this state.
		/// </summary>
		IReadOnlyDictionary<string, IState> Children { get; }

		/// <summary>
		/// All direct child components of this state.
		/// </summary>
		IReadOnlyCollection<IStateComponent> Components { get; }

		#region Hierarchy Management

		/// <summary>
		/// Sets this state's parent to be <paramref name="parent"/>.
		/// </summary>
		/// <param name="parent"></param>
		void SetParent(IState parent);

		/// <summary>
		/// Sets the state's default child which will automatically be activated with its parent.
		/// </summary>
		/// <param name="id">The ID of the default child state.</param>
		void SetDefaultChild(string id);

		/// <summary>
		/// Adds <paramref name="child"/> to this state's children.
		/// </summary>
		void AddChild(IState child);

		/// <summary>
		/// Removes <paramref name="id"/> from this state's children.
		/// </summary>
		/// <param name="id"></param>
		void RemoveChild(string id);

		#endregion Hierarchy Management
	}
}
