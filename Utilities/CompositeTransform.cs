using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class CompositeTransform : MonoBehaviour
	{
		protected Dictionary<object, Vector3> positions = new Dictionary<object, Vector3>();
		protected Dictionary<object, Quaternion> rotations = new Dictionary<object, Quaternion>();

		protected virtual void Update()
		{
			UpdateTransform();
		}

		public void AddPosition(object owner, Vector3 offset)
		{
			positions[owner] = offset;
		}

		public void AddRotation(object owner, Quaternion offset)
		{
			rotations[owner] = offset;
		}

		public void RemovePosition(object owner)
		{
			positions.Remove(owner);
		}

		public void RemoveRotation(object owner)
		{
			rotations.Remove(owner);
		}

		private void UpdateTransform()
		{
			Vector3 position = new Vector3();
			foreach (KeyValuePair<object, Vector3> kvp in positions)
			{
				position += kvp.Value;
			}

			Quaternion rotation = Quaternion.identity;
			foreach (KeyValuePair<object, Quaternion> kvp in rotations)
			{
				rotation *= kvp.Value;
			}

			transform.localRotation = rotation;
			transform.localPosition = position;
		}
	}
}
