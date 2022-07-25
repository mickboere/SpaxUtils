using SpaxUtils.StateMachine;
using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract class for instantiatable <see cref="IBehaviour"/> assets that implement <see cref="IStateComponent"/>.
	/// </summary>
	public abstract class StateBehaviourComponentAsset : BehaviourAsset, IStateComponent
	{
		/// <inheritdoc/>
		public virtual int ExecutionOrder => 0;

		/// <inheritdoc/>
		public virtual bool InjectStateDependencies => false;

		/// <inheritdoc/>
		public virtual List<IStateComponent> GetComponents()
		{
			return new List<IStateComponent>();
		}

		/// <inheritdoc/>
		public virtual void OnPrepare() { }

		/// <inheritdoc/>
		public virtual void OnEnteringState(Action onCompleteCallback) { onCompleteCallback?.Invoke(); }

		/// <inheritdoc/>
		public virtual void OnStateEntered() { }

		/// <inheritdoc/>
		public virtual void OnExitingState(Action onCompleteCallback) { onCompleteCallback?.Invoke(); }

		/// <inheritdoc/>
		public virtual void OnStateExit() { }

		/// <inheritdoc/>
		public virtual void OnUpdate() { }
	}
}
