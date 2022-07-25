using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for something holding a reference to a <see cref="UnityEngine.GameObject"/>.
	/// </summary>
	public interface IGameObject
	{
		/// <summary>
		/// The object's <see cref="UnityEngine.GameObject"/>.
		/// </summary>
		GameObject GameObject { get; }

		/// <summary>
		/// The <see cref="GameObject"/>'s <see cref="UnityEngine.Transform"/>.
		/// </summary>
		Transform Transform { get; }
	}
}