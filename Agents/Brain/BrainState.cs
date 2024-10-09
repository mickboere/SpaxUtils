using SpaxUtils.StateMachines;
using System.Collections.Generic;
using System;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IState"/> implementation to be used in <see cref="Brain"/>.
	/// </summary>
	/// <seealso cref="StateMachines"/>
	public class BrainState : IState, IDisposable
	{
		public string ID { get; private set; }
		public bool Active { get; private set; }
		public IState Parent { get; private set; }
		public IState DefaultChild => defaultChild != null && children.ContainsKey(defaultChild) ? children[defaultChild] : null;
		public IReadOnlyDictionary<string, IState> Children => children;

		public IReadOnlyCollection<IStateListener> Components => components;

		private string defaultChild;
		private Dictionary<string, IState> children;
		private List<IStateListener> components;

		private IDependencyManager dependencyManager;

		public BrainState(string id,
			IState parent = null, string defaultChild = null,
			IEnumerable<IStateListener> components = null)
		{
			ID = id;
			SetParent(parent);
			this.defaultChild = defaultChild;
			children = new Dictionary<string, IState>();
			this.components = components == null ? new List<IStateListener>() : new List<IStateListener>(components);
		}

		public virtual void Dispose()
		{
			SetParent(null);
		}

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			this.dependencyManager = dependencyManager;
			foreach (IStateListener component in components)
			{
				dependencyManager.Inject(component);
			}
		}

		#region Hierarchy Management

		/// <inheritdoc/>
		public void SetParent(IState parent)
		{
			if (parent == Parent)
			{
				return;
			}

			IState previousParent = Parent;
			Parent = parent;
			previousParent?.RemoveChild(ID);
			Parent?.AddChild(this);
		}

		/// <inheritdoc/>
		public void SetDefaultChild(string id)
		{
			defaultChild = id;
		}

		/// <inheritdoc/>
		public void AddChild(IState child)
		{
			if (!children.ContainsKey(child.ID))
			{
				children.Add(child.ID, child);
				child.SetParent(this);
			}
		}

		/// <inheritdoc/>
		public void RemoveChild(string child)
		{
			if (children.ContainsKey(child))
			{
				children[child].SetParent(null);
				children.Remove(child);
			}
		}

		#endregion

		#region Callbacks

		/// <inheritdoc/>
		public virtual void OnEnteringState()
		{
			foreach (IStateListener component in components)
			{
				component.OnEnteringState();
			}
		}

		/// <inheritdoc/>
		public virtual void WhileEnteringState(ITransition transition)
		{
			foreach (IStateListener component in components)
			{
				component.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public virtual void OnStateEntered()
		{
			Active = true;
			foreach (IStateListener component in components)
			{
				component.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public virtual void OnExitingState()
		{
			foreach (IStateListener component in components)
			{
				component.OnExitingState();
			}
		}

		/// <inheritdoc/>
		public virtual void WhileExitingState(ITransition transition)
		{
			foreach (IStateListener component in components)
			{
				component.WhileExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public virtual void OnStateExit()
		{
			Active = false;
			foreach (IStateListener component in components)
			{
				component.OnStateExit();
			}
		}

		#endregion

		#region Component Management

		/// <summary>
		/// Try to add a new <see cref="IStateListener"/> to this state.
		/// </summary>
		/// <param name="component">The <see cref="IStateListener"/> to attempt to add.</param>
		/// <returns>TRUE if it was able to be added, FALSE if it wasn't.</returns>
		public bool TryAddComponent(IStateListener component)
		{
			if (component is IState state)
			{
				SpaxDebug.Error("States cannot be added as components:", $"Tried to add state with ID={state.ID}");
				return false;
			}

			if (components.Contains(component))
			{
				return false;
			}

			components.Add(component);
			if (Active)
			{
				dependencyManager.Inject(component);
				component.OnEnteringState();
				component.OnStateEntered();
			}
			return true;
		}

		/// <summary>
		/// Try to add a collection of new <see cref="IStateListener"/>s to this state.
		/// </summary>
		/// <param name="components">The list of <see cref="IStateListener"/>s to attempt to add.</param>
		/// <returns>TRUE if it all components were successfully added, FALSE if one or multiple were not.</returns>
		public bool TryAddComponents(IEnumerable<IStateListener> components)
		{
			bool success = true;
			foreach (IStateListener component in components)
			{
				if (!TryAddComponent(component))
				{
					success = false;
				}
			}

			return success;
		}

		/// <summary>
		/// Try to remove a <see cref="IStateListener"/> from this state.
		/// </summary>
		/// <param name="component"></param>
		/// <returns></returns>
		public bool TryRemoveComponent(IStateListener component)
		{
			if (components.Contains(component))
			{
				components.Remove(component);
				if (Active)
				{
					component.OnExitingState();
					component.OnStateExit();
				}
				return true;
			}

			return false;
		}

		/// <summary>
		/// Try to remove a collection of <see cref="IStateListener"/>s from this state.
		/// </summary>
		/// <param name="components">The list of <see cref="IStateListener"/>s to try and remove.</param>
		/// <returns>TRUE if it all components were successfully removed, FALSE if one or more were not.</returns>
		public bool TryRemoveComponents(IEnumerable<IStateListener> components)
		{
			bool success = true;
			foreach (IStateListener component in components)
			{
				if (!TryRemoveComponent(component))
				{
					success = false;
				}
			}

			return success;
		}

		#endregion Component Management
	}
}
