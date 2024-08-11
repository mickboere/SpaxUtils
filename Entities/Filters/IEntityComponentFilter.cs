using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	public interface IEntityComponentFilter<T> : IDisposable where T : class, IEntityComponent
	{
		event Action<T> AddedComponentEvent;
		event Action<T> RemovedComponentEvent;

		IList<T> Components { get; }
	}
}
