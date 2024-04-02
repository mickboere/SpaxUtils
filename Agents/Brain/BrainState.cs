using SpaxUtils.StateMachines;
using System.Collections.Generic;
using System;
using System.Linq;

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

		public IReadOnlyCollection<IStateComponent> Components => components;

		private string defaultChild;
		private Dictionary<string, IState> children;
		private List<IStateComponent> components;

		private IDependencyManager dependencyManager;

		public BrainState(string id,
			IState parent = null, string defaultChild = null,
			IEnumerable<IStateComponent> components = null)
		{
			ID = id;
			SetParent(parent);
			this.defaultChild = defaultChild;
			children = new Dictionary<string, IState>();
			this.components = components == null ? new List<IStateComponent>() : new List<IStateComponent>(components);
		}

		public virtual void Dispose()
		{
			SetParent(null);
		}

		public void InjectDependencies(IDependencyManager dependencyManager)
		{
			this.dependencyManager = dependencyManager;
			foreach (IStateComponent component in components)
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

			Parent?.RemoveChild(ID);
			Parent = parent;
			Parent.AddChild(this);
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
			foreach (IStateComponent component in components)
			{
				component.OnEnteringState();
			}
		}

		/// <inheritdoc/>
		public virtual void WhileEnteringState(ITransition transition)
		{
			foreach (IStateComponent component in components)
			{
				component.WhileEnteringState(transition);
			}
		}

		/// <inheritdoc/>
		public virtual void OnStateEntered()
		{
			Active = true;
			foreach (IStateComponent component in components)
			{
				component.OnStateEntered();
			}
		}

		/// <inheritdoc/>
		public virtual void OnExitingState()
		{
			foreach (IStateComponent component in components)
			{
				component.OnExitingState();
			}
		}

		/// <inheritdoc/>
		public virtual void WhileExitingState(ITransition transition)
		{
			foreach (IStateComponent component in components)
			{
				component.WhileExitingState(transition);
			}
		}

		/// <inheritdoc/>
		public virtual void OnStateExit()
		{
			Active = false;
			foreach (IStateComponent component in components)
			{
				component.OnStateExit();
			}
		}

		#endregion

		#region Component Management

		/// <summary>
		/// Try to add a new <see cref="IStateComponent"/> to this state.
		/// </summary>
		/// <param name="component">The <see cref="IStateComponent"/> to attempt to add.</param>
		/// <returns>TRUE if it was able to be added, FALSE if it wasn't.</returns>
		public bool TryAddComponent(IStateComponent component)
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
		/// Try to remove a collection of <see cref="IStateComponent"/>s from this state.
		/// </summary>
		/// <param name="components">The list of <see cref="IStateComponent"/>s to try and remove.</param>
		/// <returns>TRUE if it all components were successfully removed, FALSE if one or more were not.</returns>
		public bool TryRemoveComponents(IEnumerable<IStateComponent> components)
		{
			bool success = true;
			foreach (IStateComponent component in components)
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
