using System;

namespace SpaxUtils
{
	/// <summary>
	/// Channel interface that can be used to send and listen for data of base type <typeparamref name="Val"/>, using type <typeparamref name="Key"/> for identifiers.
	/// </summary>
	/// <typeparam name="Key">Key/identifier type. Often <see cref="Type"/> or <see cref="string"/>.</typeparam>
	/// <typeparam name="Val">Value base type. Often an interface or simply <see cref="object"/>.</typeparam>
	public interface IChannel<Key, Val>
	{
		/// <summary>
		/// Invoked whenever any data is received.
		/// </summary>
		event Action<Key, Val> ReceivedEvent;

		/// <summary>
		/// The identifier / name of this channel, useful for debugging.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// Sends new data of type <typeparamref name="T"/> using key <paramref name="key"/>.
		/// </summary>
		/// <typeparam name="T">The value data type. Should implement <see cref="Val"/>.</typeparam>
		/// <param name="key">The data identifier.</param>
		/// <param name="val">The data object.</param>
		/// <param name="timer">The timer to store in the history, returned when calling <see cref="TryGetLast{T}(Key, out T, out TimerStruct)"/>.</param>
		void Send<T>(Key key, T val, TimerStruct timer = default) where T : Val;

		/// <summary>
		/// Adds a new listener for data of type <typeparamref name="T"/> using key <paramref name="key"/>.
		/// </summary>
		/// <typeparam name="T">The value data type. Should implement <see cref="Val"/>.</typeparam>
		/// <param name="listener">The listener object, used as identifier when unsubscribing.</param>
		/// <param name="key">The type of data to listen to.</param>
		/// <param name="callback">The callback to invoke when matching data is received.</param>
		/// <param name="catchLastWithin">Will invoke the callback if the last received <typeparamref name="Val"/> falls within the timespan.</param>
		void Listen<T>(object listener, Key key, Action<T> callback) where T : Val;

		/// <summary>
		/// Unsubscribes <paramref name="listener"/> from <paramref name="key"/> callbacks. Leave <paramref name="key"/> as <see langword="default"/> to unsubscribe from all callbacks.
		/// </summary>
		/// <param name="listener">The listener object to unsubscribe.</param>
		/// <param name="key">The specific key to unsubscribe from.</param>
		void StopListening(object listener, Key key = default);

		/// <summary>
		/// Attempt to retrieve the last received data of type <typeparamref name="T"/> using <paramref name="key"/>.
		/// </summary>
		/// <typeparam name="T">The data value type. Should implement <see cref="Val"/>.</typeparam>
		/// <param name="key">The type of data.</param>
		/// <param name="val">The resulting data value.</param>
		/// <param name="timer">The timer that was started when the data was received.</param>
		/// <returns>Whether retrieving the data last stored with <paramref name="key"/> was succesful.</returns>
		bool TryGetLast<T>(Key key, out T val, out TimerStruct timer) where T : Val;
	}
}
