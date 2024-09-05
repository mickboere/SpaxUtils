using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract base class implementing <see cref="IPooledItem"/>.
	/// </summary>
	public abstract class PooledItemBase : MonoBehaviour, IPooledItem
	{
		/// <inheritdoc/>
		public abstract bool Finished { get; }
	}
}
