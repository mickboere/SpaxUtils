using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="ICommunicationChannel"/> implementation for basic communication.
	/// </summary>
	public class CommunicationChannel : ChannelBase<Type, object>, ICommunicationChannel
	{
		public const string DEFAULT_COMMS_IDENTIFIER = "COMMS";

		public CommunicationChannel() : base(DEFAULT_COMMS_IDENTIFIER) { }

		public CommunicationChannel(string identifier = DEFAULT_COMMS_IDENTIFIER) : base(identifier) { }

		/// <inheritdoc/>
		public void Send<T>(T message, TimerStruct timer = default)
		{
			Type key = typeof(T);
			history[key] = (message, timer);
			OnReceived(key, message);

			foreach (KeyValuePair<Type, Dictionary<object, Action<object>>> subscription in subscriptions)
			{
				if (subscription.Key.IsAssignableFrom(key))
				{
					var listeners = new Dictionary<object, Action<object>>(subscription.Value); // This fixes unsub during callback bug.
					foreach (KeyValuePair<object, Action<object>> listener in listeners)
					{
						listener.Value(message);
					}
				}
			}
		}

		/// <inheritdoc/>
		public void Listen<T>(object listener, Action<T> callback)
		{
			base.Listen<T>(listener, typeof(T), callback);
		}

		/// <inheritdoc/>
		public void Link(ICommunicationChannel communicationChannel)
		{
			if (communicationChannel == this)
			{
				return;
			}
			communicationChannel.ReceivedEvent += OnLinkedMessageReceived;
		}

		/// <inheritdoc/>
		public void Unlink(ICommunicationChannel communicationChannel)
		{
			if (communicationChannel == this)
			{
				return;
			}
			communicationChannel.ReceivedEvent -= OnLinkedMessageReceived;
		}

		private void OnLinkedMessageReceived(Type t, object o)
		{
			Send(t, o);
		}
	}
}
