namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Interface for objects that need to listen to callbacks relating to the current state.
	/// </summary>
	public interface IStateListener
	{
		public enum Callback
		{
			OnEnteringState,
			WhileEnteringState,
			OnStateEntered,
			OnExitingState,
			WhileExitingState,
			OnStateExit
		}

		/// <summary>
		/// Called once before entering a state.
		/// </summary>
		void OnEnteringState();

		/// <summary>
		/// Called every frame while entering the state.
		/// </summary>
		void WhileEnteringState(ITransition transition);

		/// <summary>
		/// Called when the state containing this component is entered.
		/// </summary>
		void OnStateEntered();

		/// <summary>
		/// Called once before exiting a state.
		/// </summary>
		void OnExitingState();

		/// <summary>
		/// Called every frame while the state is being exited.
		/// </summary>
		void WhileExitingState(ITransition transition);

		/// <summary>
		/// Called when the state containing this node has exited.
		/// </summary>
		void OnStateExit();
	}
}
