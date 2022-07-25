using System;

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
		public void Send<T>(T message, Timer timer = default)
		{
			base.Send<T>(typeof(T), message, timer);
		}

		/// <inheritdoc/>
		public void Listen<T>(object listener, Action<T> callback)
		{
			base.Listen<T>(listener, typeof(T), callback);
		}
	}
}
