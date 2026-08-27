using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Works out where a limb's middle joint has to be for a given tip position: on the ring of places its
	/// two bones can actually reach, turned around that ring toward whichever way the joint should bend.
	/// </summary>
	public static class LimbHint
	{
		/// <summary>
		/// Where the joint lands with the tip at <paramref name="tipPosition"/>, bent toward
		/// <paramref name="roll"/> as expressed in <paramref name="frame"/> space.
		/// </summary>
		public static bool TrySolve(Transform root, Transform joint, Transform tip,
			Vector3 tipPosition, Quaternion frame, Vector3 roll, out Vector3 hint)
		{
			hint = Vector3.zero;
			if (!TryGetCircle(root, joint, tip, tipPosition, out Vector3 centre, out float radius, out Vector3 axis))
			{
				return false;
			}

			if (!TryGetRoll(frame, roll, axis, out Vector3 direction))
			{
				return false;
			}

			hint = centre + direction * radius;
			return true;
		}

		/// <summary>
		/// Every place the two bones can put the joint with the tip at <paramref name="tipPosition"/>: a
		/// circle about the root-to-tip line. Anywhere off it is a pose the limb cannot make.
		/// </summary>
		public static bool TryGetCircle(Transform root, Transform joint, Transform tip, Vector3 tipPosition,
			out Vector3 centre, out float radius, out Vector3 axis)
		{
			centre = Vector3.zero;
			radius = 0f;
			axis = Vector3.forward;

			if (root == null || joint == null || tip == null)
			{
				return false;
			}

			float upper = Vector3.Distance(root.position, joint.position);
			float lower = Vector3.Distance(joint.position, tip.position);

			Vector3 span = tipPosition - root.position;
			float reach = Mathf.Min(span.magnitude, upper + lower);
			if (reach < Mathf.Epsilon)
			{
				return false;
			}

			// How far along the limb the joint falls, and how far off that line it stands.
			axis = span.normalized;
			float along = Mathf.Clamp((upper * upper - lower * lower + reach * reach) / (2f * reach), 0f, reach);

			centre = root.position + axis * along;
			radius = Mathf.Sqrt(Mathf.Max(upper * upper - along * along, 0f));
			return true;
		}

		/// <summary>
		/// A bend preference resolved against the limb's current line, as a direction on its solution circle.
		/// </summary>
		public static bool TryGetRoll(Quaternion frame, Vector3 roll, Vector3 axis, out Vector3 direction)
		{
			direction = Vector3.zero;
			if (roll == Vector3.zero)
			{
				return false;
			}

			Vector3 projected = Vector3.ProjectOnPlane(frame * roll, axis);
			if (projected.sqrMagnitude < Mathf.Epsilon)
			{
				return false;
			}

			direction = projected.normalized;
			return true;
		}
	}
}
