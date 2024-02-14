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
		bool Blocked { get; set; }

		/// <summary>
		/// Sends a new act to the actor.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="act"></param>
		/// <param name="timer"></param>
		void Send<T>(T act, TimerStruct timer = default) where T : IAct;

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
