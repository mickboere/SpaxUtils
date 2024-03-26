using System;

namespace SpaxUtils.StateMachines
{
	/// <summary>
	/// Interface containing basic (state) transition data.
	/// </summary>
	public interface ITransition : IDisposable
	{
		/// <summary>
		/// Float ranging from 0 to 1 expressing the state entry transition progress.
		/// </summary>
		float EntryProgress { get; }

		/// <summary>
		/// Float ranging from 0 to 1 expressing the state exit transition progress (the inverse of <see cref="EntryProgress"/>).
		/// </summary>
		float ExitProgress { get; }

		/// <summary>
		/// Whether the transition has completed.
		/// </summary>
		bool Completed { get; }
	}
}
