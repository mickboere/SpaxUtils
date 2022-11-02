using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract class for instantiatable <see cref="IBehaviour"/> assets.
	/// </summary>
	public abstract class BehaviorAsset : ScriptableObject, IBehaviour
	{
		/// <inheritdoc/>
		public bool Running { get; private set; }

		/// <inheritdoc/>
		public virtual void Start()
		{
			Running = true;
		}

		/// <inheritdoc/>
		public virtual void Stop()
		{
			Running = false;
		}

		/// <summary>
		/// Returns a new instance of this behaviour.
		/// </summary>
		public virtual BehaviorAsset CreateInstance()
		{
			return Instantiate(this);
		}

		/// <summary>
		/// Stops and destroys this behaviour.
		/// </summary>
		public virtual void Destroy()
		{
			if (Running)
			{
				Stop();
			}

			Destroy(this);
		}
	}
}
