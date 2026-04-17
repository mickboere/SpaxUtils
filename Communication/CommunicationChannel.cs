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

		public CommunicationChannel(string identifier = DEFAULT_COMMS_IDENTIFIER, bool debug = false) : base(identifier, debug) { }

		/// <inheritdoc/>
		public void Send<T>(T message, TimerStruct timer = default)
		{
			Type key = typeof(T);
			history[key] = (message, timer);
			OnReceived(key, message);

			var subscriptionsCopy = new Dictionary<Type, Dictionary<object, Action<object>>>(subscriptions);
			foreach (KeyValuePair<Type, Dictionary<object, Action<object>>> subscription in subscriptionsCopy)
			{
				if (subscription.Key.IsAssignableFrom(key))
				{
					var listeners = new Dictionary<object, Action<object>>(subscription.Value);
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
	}
}
