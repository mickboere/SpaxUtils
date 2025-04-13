using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for option request listeners not interested in the target type.
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

	/// <summary>
	/// Interface for option request listeners interested in a target type.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface IRequestOptionsMsg<T> : IRequestOptionsMsg
	{
		T Target { get; }
	}
}
