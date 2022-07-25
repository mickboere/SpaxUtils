using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that contains data needed for targetting this <see cref="IEntity"/>.
	/// </summary>
	public interface ITargetable : IEntityComponent
	{
		#region Properties

		/// <summary>
		/// The main look-at point of this targetable.
		/// </summary>
		Vector3 Point { get; }

		/// <summary>
		/// The position of the targetable.
		/// </summary>
		Vector3 Position { get; }

		/// <summary>
		/// The rotation of the targetable.
		/// </summary>
		Quaternion Rotation { get; }

		/// <summary>
		/// Bounds of the targetable.
		/// </summary>
		Bounds Bounds { get; }

		/// <summary>
		/// The center point of the targetable.
		/// </summary>
		Vector3 Center { get; }

		/// <summary>
		/// The size of the targetable.
		/// </summary>
		Vector3 Size { get; }

		/// <summary>
		/// Returns whether this <see cref="ITargetable"/> is currently targetable.
		/// </summary>
		bool IsTargetable { get; }

		#endregion
	}
}
