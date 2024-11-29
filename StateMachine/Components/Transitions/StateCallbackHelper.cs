using System;
using System.Collections.Generic;

namespace SpaxUtils.StateMachines
{
	public class StateCallbackHelper : IDisposable
	{
		public bool Running { get; private set; }

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
			if (Running)
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
			Running = true;
		}

		public void QuickExit()
		{
			if (!Running)
			{
				return;
			}
			foreach (IStateListener listener in listeners)
			{
				listener.OnExitingState(null);
				listener.WhileExitingState(null);
				listener.OnStateExit();
			}
			Running = false;
		}

		#region Callbacks

		public void OnEnteringState(ITransition transition)
		{
			if (Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.OnEnteringState(transition);
			}
			Running = true;
		}

		public void WhileEnteringState(ITransition transition)
		{
			if (Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.WhileEnteringState(transition);
			}
		}

		public void OnStateEntered()
		{
			if (Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.OnStateEntered();
			}
		}

		public void OnExitingState(ITransition transition)
		{
			if (!Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.OnExitingState(transition);
			}
		}

		public void WhileExitingState(ITransition transition)
		{
			if (!Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.WhileExitingState(transition);
			}
		}

		public void OnStateExit()
		{
			if (!Running)
			{
				return;
			}
			foreach (IStateListener component in listeners)
			{
				component.OnStateExit();
			}
			Running = false;
		}

		#endregion Callbacks
	}
}
