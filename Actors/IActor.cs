using System;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for objects that can perform exclusive <see cref="IAct"/>s.
	/// </summary>
	public interface IActor : IChannel<string, IAct>, IPerformer
	{
		/// <summary>
		/// Sends a new act to the actor.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="act"></param>
		/// <param name="timer"></param>
		void Send<T>(T act, Timer timer = default) where T : IAct;

		/// <summary>
		/// Add a new <see cref="IPerformer"/> able to take control and execute <see cref="IAct"/>s on behalf of the Agent.
		/// </summary>
		void AddPerformer(IPerformer performer);

		/// <summary>
		/// Removes a registered performer.
		/// </summary>
		void RemovePerformer(IPerformer performer);
	}
}
