using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// A message to be sent over a <see cref="ICommunicationChannel"/>.
	/// Will request <see cref="Option"/>s for its target of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The target type for which options are being requested.</typeparam>
	public class RequestOptionsMsg<T> : IRequestOptionsMsg, IDisposable
	{
		public event Action ClosedRequestEvent;

		public T Target { get; }
		public string Context { get; }
		public IReadOnlyList<Option> Options => options;
		public bool Closed { get; private set; }

		private List<Option> options = new List<Option>();

		public RequestOptionsMsg(T target, string context)
		{
			Target = target;
			Context = context;
			Closed = false;
		}

		public void AddOption(Option option)
		{
			if (Closed)
			{
				SpaxDebug.Error("Tried to add option but the request is closed.\n", option.ToString());
				return;
			}

			options.Add(option);
		}

		public void CloseRequest()
		{
			if (Closed)
			{
				return;
			}

			Closed = true;
			ClosedRequestEvent?.Invoke();
		}

		public void Dispose()
		{
		}

		#region Static Methods

		/// <summary>
		/// Creates a new request for type <typeparamref name="T"/>, sends it through the <paramref name="comms"/>, and finally closes the request.
		/// <para>Method isn't a constructor because it does more than just construct the request.</para>
		/// </summary>
		/// <param name="target">The target object in question, of which to request <see cref="Option"/>s for.</param>
		/// <param name="comms">The <see cref="ICommunicationChannel"/> to send the request through.</param>
		/// <param name="defaultOptions">The options that should be available regardless of any comms listeners.</param>
		/// <returns>The completed request.</returns>
		public static RequestOptionsMsg<T> New(T target, string context, ICommunicationChannel comms, params Option[] defaultOptions)
		{
			var request = new RequestOptionsMsg<T>(target, context);

			// Add the default options.
			foreach (Option option in defaultOptions)
			{
				request.AddOption(option);
			}

			// Listeners of the msg should add their options the moment the msg is received.
			// This means we can immediately close the request after sending it.
			comms.Send(request);
			request.CloseRequest();

#if UNITY_EDITOR
			// Validate that no options are conflicting on a base level.
			// This does not impact the request, it is only useful for developers.
			ValidateOptions(request.Options);
#endif

			return request;
		}

		/// <summary>
		/// Validates that no options are conflicting on a base level.
		/// </summary>
		/// <param name="options">The options to validate.</param>
		/// <returns>FALSE when options are conflicting, TRUE when options aren't.</returns>
		public static bool ValidateOptions(IReadOnlyCollection<Option> options)
		{
			// Check for conflicting input commands.
			List<Option> conflicts =
				options.Where(
					(a) => options.Any(
						(b) =>
							a != b &&
							!string.IsNullOrEmpty(a.InputAction) &&
							a.InputAction == b.InputAction))
				.ToList();

			if (conflicts.Count > 0)
			{
				SpaxDebug.Error("Conflicting options.", string.Join("___\n", conflicts));
				return false;
			}

			return true;
		}

		#endregion
	}

	/// <summary>
	/// Interface for listeners not interested in the target type.
	/// </summary>
	public interface IRequestOptionsMsg
	{
		event Action ClosedRequestEvent;

		string Context { get; }
		IReadOnlyList<Option> Options { get; }
		bool Closed { get; }

		void AddOption(Option option);
		void CloseRequest();
	}
}
