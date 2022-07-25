using SpaxUtils.StateMachine;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IState"/> implementation for states within a <see cref="Brain"/>.
	/// </summary>
	public class BrainState : IBrainState, IDisposable
	{
		public string Name => UID;
		public string UID { get; private set; }
		public int ExecutionOrder => int.MinValue;
		public bool InjectStateDependencies => false;
		public BrainState ParentState { get; private set; }
		public IReadOnlyList<IBrainState> SubStates => subStates;

		private List<IStateComponent> components = new List<IStateComponent>();
		private List<IBrainState> subStates = new List<IBrainState>();

		public BrainState(string stateIdentifier, BrainState parentState = null, IEnumerable<BrainState> subStates = null, IEnumerable<IStateComponent> components = null)
		{
			UID = stateIdentifier;
			ParentState = parentState;
			if (subStates != null)
			{
				this.subStates = new List<IBrainState>(subStates);
			}
			if (components != null)
			{
				this.components = new List<IStateComponent>(components);
			}
		}

		public void Dispose()
		{
			components.Clear();
		}

		#region Parenting

		/// <summary>
		/// Attempt to set the parent of this brainstate.
		/// </summary>
		public bool TrySetParent(BrainState parentState, bool overrideCurrent = false)
		{
			if (ParentState == null || overrideCurrent)
			{
				ParentState = parentState;

				if (!ParentState.ContainsSubState(this))
				{
					ParentState.TryAddSubState(this);
				}

				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public IBrainState GetParentState()
		{
			return ParentState;
		}

		public List<string> GetStateHierarchy()
		{
			// If there is no parent return a new list containing only this state.
			if (ParentState == null)
			{
				return new List<string>() { Name };
			}

			// Retrieve the parent's hierarchy and append this state.
			List<string> hierarchy = ParentState.GetStateHierarchy();
			hierarchy.Add(Name);
			return hierarchy;
		}

		#endregion

		#region Substates

		/// <summary>
		/// Attempt to add a sub state to this brainstate.
		/// </summary>
		public bool TryAddSubState(BrainState subState)
		{
			if (subStates.Contains(subState))
			{
				return false;
			}

			subStates.Add(subState);

			// Force update sub state's parent.
			if (subState.ParentState != this)
			{
				subState.TrySetParent(this, true);
			}

			return true;
		}

		/// <summary>
		/// Attempt to remove a sub state from this brainstate.
		/// </summary>
		public bool TryRemoveSubState(BrainState subState)
		{
			if (!subStates.Contains(subState))
			{
				return false;
			}

			subStates.Remove(subState);
			return true;
		}

		/// <summary>
		/// Check whether this brainstate contains the given substate.
		/// </summary>
		public bool ContainsSubState(BrainState subState, bool indirectly = false)
		{
			if (subStates.Contains(subState))
			{
				return true;
			}

			if (indirectly)
			{
				foreach (BrainState s in subStates)
				{
					if (s.ContainsSubState(subState, indirectly))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <inheritdoc/>
		public List<IBrainState> GetSubStates()
		{
			return subStates;
		}

		#endregion

		#region Components

		/// <summary>
		/// Try to add a new <see cref="IStateComponent"/> to this state.
		/// </summary>
		/// <param name="component">The <see cref="IStateComponent"/> to attempt to add.</param>
		/// <returns>TRUE if it was able to be added, FALSE if it wasn't.</returns>
		public bool TryAddComponent(IStateComponent component)
		{
			if (component is IState)
			{
				SpaxDebug.Error("Could not add component to BrainState. ", "State components cannot be of type IState.");
				return false;
			}

			if (components.Contains(component))
			{
				return false;
			}

			components.Add(component);
			return true;
		}

		/// <summary>
		/// Try to add a collection of new <see cref="IStateComponent"/>s to this state.
		/// </summary>
		/// <param name="components">The list of <see cref="IStateComponent"/>s to attempt to add.</param>
		/// <returns>TRUE if it all components were successfully added, FALSE if one or multiple were not.</returns>
		public bool TryAddComponents(IEnumerable<IStateComponent> components)
		{
			bool success = true;
			foreach (IStateComponent component in components)
			{
				if (!TryAddComponent(component))
				{
					success = false;
				}
			}

			return success;
		}

		/// <summary>
		/// Try to remove a <see cref="IStateComponent"/> from this state.
		/// </summary>
		/// <param name="component"></param>
		/// <returns></returns>
		public bool TryRemoveComponent(IStateComponent component)
		{
			if (components.Contains(component))
			{
				components.Remove(component);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public List<IStateComponent> GetComponents()
		{
			return new List<IStateComponent>(components);
		}

		/// <inheritdoc/>
		public virtual List<IStateComponent> GetAllComponents()
		{
			List<IStateComponent> components = new List<IStateComponent>();
			components.AddRange(this.GetAllComponentsInParents());
			components.AddRange(this.GetAllChildComponents());
			return components;
		}

		#endregion

		#region Callbacks

		public virtual void OnPrepare() { }

		public virtual void OnEnteringState(Action onCompleteCallback) { onCompleteCallback?.Invoke(); }

		public virtual void OnStateEntered() { }

		public virtual void OnExitingState(Action onCompleteCallback) { onCompleteCallback?.Invoke(); }

		public virtual void OnStateExit() { }

		public virtual void OnUpdate() { }

		#endregion
	}
}
