using System;
using System.Collections.Generic;

namespace SpaxUtils.StateMachines
{
	public class StateCallbackHelper : IDisposable
	{
		/// <summary>
		/// Whether the listeners are currently activated (the parent state is entered/active).
		/// </summary>
		public bool Active { get; private set; }

		private readonly IDependencyManager dependencyManager;
		private readonly IEnumerable<IStateListener> listeners;

		public StateCallbackHelper(IDependencyManager dependencyManager, IState state, IEnumerable<IStateListener> listeners)
		{
			this.dependencyManager = dependencyManager;
			this.listeners = listeners;

			foreach (IStateListener component in listeners)
			{
				component.Initialize(state);
			}
		}

		public void Dispose()
		{
		}

		public void Inject(IDependencyManager dependencyManager = null)
		{
			dependencyManager = dependencyManager ?? this.dependencyManager;
			foreach (IStateListener component in listeners)
			{
				dependencyManager.Inject(component);
			}
		}

		public void QuickEnter(bool inject = true)
		{
			if (Active)
			{
				return;
			}
			foreach (IStateListener listener in listeners)
			{
				if (inject)
				{
					dependencyManager.Inject(listener);
				}
				listener.OnEnteringState(null);
				listener.WhileEnteringState(null);
				listener.OnStateEntered();
			}
			Active = true;
		}

		public void QuickExit()
		{
			if (!Active)
			{
				return;
			}
			foreach (IStateListener listener in listeners)
			{
				listener.OnExitingState(null);
				listener.WhileExitingState(null);
				listener.OnStateExit();
			}
			Active = false;
		}

		#region Callbacks

		public void OnEnteringState(ITransition transition)
		{
			foreach (IStateListener component in listeners)
			{
				component.OnEnteringState(transition);
			}
		}

		public void WhileEnteringState(ITransition transition)
		{
			foreach (IStateListener component in listeners)
			{
				component.WhileEnteringState(transition);
			}
		}

		public void OnStateEntered()
		{
			foreach (IStateListener component in listeners)
			{
				component.OnStateEntered();
			}
			Active = true;
		}

		public void OnExitingState(ITransition transition)
		{
			foreach (IStateListener component in listeners)
			{
				component.OnExitingState(transition);
			}
		}

		public void WhileExitingState(ITransition transition)
		{
			foreach (IStateListener component in listeners)
			{
				component.WhileExitingState(transition);
			}
		}

		public void OnStateExit()
		{
			foreach (IStateListener component in listeners)
			{
				component.OnStateExit();
			}
			Active = false;
		}

		#endregion Callbacks
	}
}
