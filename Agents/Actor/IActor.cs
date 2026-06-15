using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can perform exclusive <see cref="IAct"/>s.
	/// </summary>
	public interface IActor : IChannel<string, IAct>, IPerformer
	{
		/// <summary>
		/// The upper-most active performer.
		/// </summary>
		IPerformer MainPerformer { get; }

		/// <summary>
		/// Whether the actor is currently blocked from performing.
		/// </summary>
		bool Blocked { get; }

		/// <summary>
		/// Sends a new act to the actor.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="act"></param>
		/// <param name="timer"></param>
		void Send<T>(T act, TimerStruct timer = default) where T : IAct;

		/// <summary>
		/// Send (button) input to this actor in the form of a boolean act.
		/// </summary>
		/// <param name="act">The name of the act to perform.</param>
		/// <param name="value">The (button) value for the act. TRUE=hold, FALSE=release.
		/// If <paramref name="value"/> is null, a full button press will be stimulated by sending a TRUE followed immediately by a FALSE.</param>
		/// <param name="callback">The callback to be invoked once the input has been accepted by the actor to be performed.</param>
		/// <param name="buffer">Optional override for the act's input-buffer duration (seconds). NULL uses the act/mapping default. 0 = fire-or-drop: a rejected attempt is not retained or retried — use it for programmatic (AI) inputs that re-decide every frame and must not linger past the moment they were sent.</param>
		void SendInput(string act, bool? value = null, Action<IPerformer> callback = null, float? buffer = null);

		/// <summary>
		/// Add a new <see cref="IPerformer"/> able to take control and execute <see cref="IAct"/>s on behalf of the Agent.
		/// </summary>
		void AddPerformer(IPerformer performer);

		/// <summary>
		/// Removes a registered performer.
		/// </summary>
		void RemovePerformer(IPerformer performer);

		/// <summary>
		/// Make <paramref name="blocker"/> block this actor from performing.
		/// </summary>
		void AddBlocker(object blocker);

		/// <summary>
		/// Remove <paramref name="blocker"/> from preventing this actor from performing.
		/// </summary>
		/// <param name="blocker"></param>
		void RemoveBlocker(object blocker);
	}
}
