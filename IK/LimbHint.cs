using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Where a limb's middle joint can be for a given tip position: the ring of places its two bones can
	/// actually reach. Which way round that ring the joint should sit is the caller's to decide.
	/// </summary>
	public static class LimbHint
	{
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

	}
}
