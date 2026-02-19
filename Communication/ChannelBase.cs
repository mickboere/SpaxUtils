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

		/// <inheritdoc/>
		public bool Debug { get; set; }

		protected Dictionary<Key, Dictionary<object, Action<Val>>> subscriptions;
		protected Dictionary<Key, (Val, TimerStruct)> history;
		protected List<IChannel<Key, Val>> links;
		protected bool sending;

		public ChannelBase(string identifier, bool debug = false)
		{
			Identifier = identifier;
			subscriptions = new Dictionary<Key, Dictionary<object, Action<Val>>>();
			history = new Dictionary<Key, (Val, TimerStruct)>();
			links = new List<IChannel<Key, Val>>();
			Debug = debug;
		}

		/// <inheritdoc/>
		public virtual void Send<T>(Key key, T val, TimerStruct timer = default) where T : Val
		{
			history[key] = (val, timer);

			OnReceived(key, val);

			if (subscriptions.TryGetValue(key, out Dictionary<object, Action<Val>> dict) && dict != null)
			{
				var listeners = new Dictionary<object, Action<Val>>(dict);
				foreach (KeyValuePair<object, Action<Val>> listener in listeners)
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
			if (listener == null || subscriptions == null)
			{
				return;
			}

			// Treat default(Key) as "no key supplied" and remove from all keys.
			bool removeFromAll = EqualityComparer<Key>.Default.Equals(key, default(Key));

			if (removeFromAll)
			{
				// Copy keys to avoid modification-during-enumeration issues.
				List<Key> keys = new List<Key>(subscriptions.Keys);
				for (int i = 0; i < keys.Count; i++)
				{
					Key k = keys[i];

					if (!subscriptions.TryGetValue(k, out Dictionary<object, Action<Val>> dict) || dict == null)
					{
						subscriptions.Remove(k);
						continue;
					}

					dict.Remove(listener);

					if (dict.Count == 0)
					{
						subscriptions.Remove(k);
					}
				}
			}
			else
			{
				// Remove listener only from the given key.
				if (subscriptions.TryGetValue(key, out Dictionary<object, Action<Val>> dict) && dict != null)
				{
					dict.Remove(listener);
					if (dict.Count == 0)
					{
						subscriptions.Remove(key);
					}
				}
			}
		}

		/// <inheritdoc/>
		public virtual bool TryGetLast<T>(Key key, out T val, out TimerStruct timer) where T : Val
		{
			val = default;
			timer = default;

			if (!history.ContainsKey(key))
			{
				return false;
			}

			(Val val, TimerStruct timer) tuple = history[key];
			val = (T)tuple.val;
			timer = tuple.timer;
			return true;
		}

		/// <inheritdoc/>
		public void Link(IChannel<Key, Val> channel)
		{
			if (channel == this || links.Contains(channel)) { return; }

			links.Add(channel);
			channel.ReceivedEvent += OnLinkedMessageReceived;
		}

		/// <inheritdoc/>
		public void Unlink(IChannel<Key, Val> channel)
		{
			if (channel == this || !links.Contains(channel)) { return; }

			links.Remove(channel);
			channel.ReceivedEvent -= OnLinkedMessageReceived;
		}

		/// <summary>
		/// Invoked when the channel has received a new message event through <see cref="Send{T}(Key, T, TimerStruct)"/>.
		/// </summary>
		/// <param name="key">The variable identifying the message type.</param>
		/// <param name="value">The variable containing the message.</param>
		protected virtual void OnReceived(Key key, Val value)
		{
			if (Debug)
			{
				SpaxDebug.Log(Identifier, $"key={key}, val={value}");
			}

			sending = true;
			ReceivedEvent?.Invoke(key, value);
			sending = false;
		}

		/// <summary>
		/// Invoked when a linked channel has received a new message event, then forwards that message over this channel.
		/// </summary>
		/// <param name="key">The variable identifying the message type.</param>
		/// <param name="value">The variable containing the message.</param>
		protected virtual void OnLinkedMessageReceived(Key key, Val value)
		{
			if (!sending) // Prevent feedback loop.
			{
				Send(key, value);
			}
		}
	}
}
