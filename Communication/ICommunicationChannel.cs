using System;

namespace SpaxUtils
{
	/// <summary>
	/// Hyper generic <see cref="IChannel{Key, Val}"/> interface that uses the value type as key.
	/// </summary>
	public interface ICommunicationChannel : IChannel<Type, object>
	{
		/// <summary>
		/// Send a new message using the message type as key.
		/// </summary>
		/// <typeparam name="T">The type of message.</typeparam>
		/// <param name="message">The message value object.</param>
		/// <param name="timer">The timer associated to this message.</param>
		void Send<T>(T message, Timer timer = default);

		/// <summary>
		/// Have <paramref name="listener"/> subscribe to messages of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type of message to listen for.</typeparam>
		/// <param name="listener">The listener identifier.</param>
		/// <param name="callback">The callback to invoke upon receiving a message of type <typeparamref name="T"/>.</param>
		void Listen<T>(object listener, Action<T> callback);
	}
}
