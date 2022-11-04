using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Basic <see cref="IEquipmentSlot"/> implementation with no unique inherent qualities.
	/// </summary>
	[Serializable]
	public class EquipmentSlot : IEquipmentSlot
	{
		/// <inheritdoc/>
		public virtual string ID { get; private set; }

		/// <inheritdoc/>
		public virtual string Type { get; private set; }

		/// <inheritdoc/>
		public virtual Transform Parent { get; private set; }

		private Func<(Vector3 pos, Quaternion rot)> getOrientationFunc;

		public EquipmentSlot(string uid, string type, Transform parent = null,
			Func<(Vector3 pos, Quaternion rot)> getOrientationFunc = null)
		{
			ID = uid;
			Type = type;
			Parent = parent;
			this.getOrientationFunc = getOrientationFunc;
		}

		/// <inheritdoc/>
		public virtual (Vector3 pos, Quaternion rot) GetOrientation()
		{
			if (getOrientationFunc != null)
			{
				return getOrientationFunc();
			}

			return (Vector3.zero, Quaternion.identity);
		}
	}
}
