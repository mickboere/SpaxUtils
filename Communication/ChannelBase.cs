using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Generic implementation of a <see cref="IChannel{Key, Val}"/>.
	/// </summary>
	public abstract class ChannelBase<Key, Val> : IChannel<Key, Val>
	{
		/// <inheritdoc/>
		public event Action<Key, Val> ReceivedEvent;

		/// <inheritdoc/>
		public string Identifier { get; }

		private Dictionary<Key, Dictionary<object, Action<Val>>> subscriptions;
		private Dictionary<Key, (Val, Timer)> history;

		public ChannelBase(string identifier)
		{
			Identifier = identifier;
			subscriptions = new Dictionary<Key, Dictionary<object, Action<Val>>>();
			history = new Dictionary<Key, (Val, Timer)>();
		}

		/// <inheritdoc/>
		public virtual void Send<T>(Key key, T val, Timer timer = default) where T : Val
		{
			history[key] = (val, timer);

			OnReceived(key, val);
			ReceivedEvent?.Invoke(key, val);

			if (subscriptions.ContainsKey(key))
			{
				foreach (KeyValuePair<object, Action<Val>> listener in subscriptions[key])
				{
					listener.Value(val);
				}
			}
		}

		/// <inheritdoc/>
		public virtual void Listen<T>(object listener, Key key, Action<T> callback) where T : Val
		{
			if (!subscriptions.ContainsKey(key))
			{
				subscriptions.Add(key, new Dictionary<object, Action<Val>>());
			}

			subscriptions[key][listener] = (Val val) => callback((T)val);
		}

		/// <inheritdoc/>
		public virtual void StopListening(object listener, Key key = default)
		{
			if (key == null || default(Key).Equals(key))
			{
				// Remove listener from all keys.
				foreach (KeyValuePair<Key, Dictionary<object, Action<Val>>> kvp in subscriptions)
				{
					kvp.Value.Remove(listener);
				}
			}
			else
			{
				// Remove listener only from given key.
				subscriptions.Remove(key);
			}
		}

		/// <inheritdoc/>
		public virtual bool TryGetLast<T>(Key key, out T val, out Timer timer) where T : Val
		{
			val = default;
			timer = default;

			if (!history.ContainsKey(key))
			{
				return false;
			}

			(Val val, Timer timer) tuple = history[key];
			val = (T)tuple.val;
			timer = tuple.timer;
			return true;
		}

		/// <summary>
		/// Invoked when the channel has received a new message event through <see cref="Send{T}(Key, T, Timer)"/>.
		/// </summary>
		/// <param name="key">The variable identifying the message type.</param>
		/// <param name="value">The variable containing the message.</param>
		protected virtual void OnReceived(Key key, Val value) { }
	}
}
