using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for components that are able to be pooled.
	/// </summary>
	public interface IPooledItem
	{
		/// <summary>
		/// The default size of pools utilizing this item.
		/// </summary>
		int DefaultPoolSize { get; }

		/// <summary>
		/// Whether this pooled item's task is finished and is ready to be returned to the pool.
		/// </summary>
		bool Finished { get; }
	}
}
