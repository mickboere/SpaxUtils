using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The arm swap's pure helpers: the torso reduced to capsules, the search round an elbow's ring, and
	/// the small questions asked of an armament. Nothing here reads or writes any arm's own state.
	/// </summary>
	public static class ArmUtils
	{
		/// <summary>How the elbow's ring is searched for the body's edge: coarse walk, then bisection onto it.</summary>
		public const float CLEAR_STEP = 2f;
		public const int CLEAR_BISECTIONS = 6;

		/// <summary>The bones whose own colliders the arc is kept clear of. Limbs and head are not in its way.</summary>
		public static readonly string[] TORSO_BONES =
		{
			HumanBoneIdentifiers.HIPS, HumanBoneIdentifiers.SPINE,
			HumanBoneIdentifiers.CHEST, HumanBoneIdentifiers.UPPER_CHEST
		};

		#region Body shape

		/// <summary>
		/// One collider reduced to its two end-sphere centres and a radius, in the body's frame. Scale is
		/// read the way Unity reads it: radius off the two axes across, length off the one along.
		/// </summary>
		public static bool Capsule(CapsuleCollider capsule, Quaternion inverse, Vector3 origin,
			out BodyCapsule shape)
		{
			shape = default;
			Transform owner = capsule.transform;
			Vector3 scale = owner.lossyScale;
			Vector3 absolute = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

			Vector3 axis;
			float radiusScale;
			float lengthScale;
			switch (capsule.direction)
			{
				case 2: axis = Vector3.forward; radiusScale = Mathf.Max(absolute.x, absolute.y); lengthScale = absolute.z; break;
				case 1: axis = Vector3.up; radiusScale = Mathf.Max(absolute.x, absolute.z); lengthScale = absolute.y; break;
				default: axis = Vector3.right; radiusScale = Mathf.Max(absolute.y, absolute.z); lengthScale = absolute.x; break;
			}

			float radius = capsule.radius * radiusScale;
			if (radius <= 0f)
			{
				return false;
			}

			// Unity clamps a capsule's height to its own diameter, so the segment can be nothing at all.
			float half = Mathf.Max(0f, Mathf.Max(capsule.height * lengthScale, radius * 2f) * 0.5f - radius);
			Vector3 centre = owner.TransformPoint(capsule.center);
			Vector3 along = owner.rotation * axis * half;

			shape.Start = inverse * (centre - along - origin);
			shape.End = inverse * (centre + along - origin);
			shape.Radius = radius;
			return true;
		}

		/// <summary>
		/// Whether the elbow at <paramref name="direction"/> round its ring is out of the body. The frame
		/// is passed in: the ring is walked a couple of hundred times a frame, and reading it is not free.
		/// </summary>
		public static bool ClearsBody(BodyCapsule[] capsules, Vector3 centre, float radius,
			Vector3 direction, float offset, Vector3 origin, Quaternion inverse)
		{
			Vector3 local = inverse * (centre + direction * radius - origin);

			for (int i = 0; i < capsules.Length; i++)
			{
				if (Vector3.Distance(local, capsules[i].Closest(local)) < capsules[i].Radius + offset)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether any point on the ring at all is clear, which decides if the margin is affordable.</summary>
		public static bool AnyClear(BodyCapsule[] capsules, Vector3 centre, float radius, Vector3 axis,
			Vector3 from, float offset, Vector3 origin, Quaternion inverse)
		{
			for (float turn = 0f; turn < 360f; turn += CLEAR_STEP)
			{
				if (ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(turn, axis) * from,
					offset, origin, inverse))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The nearest point round the ring that is clear of the body, searched outwards from
		/// <paramref name="from"/> so the elbow never crosses to the far side to find one.
		/// </summary>
		public static bool TryNearestClear(BodyCapsule[] capsules, Vector3 centre, float radius,
			Vector3 axis, Vector3 from, float toward, float offset, Vector3 origin, Quaternion inverse,
			out Vector3 nearest)
		{
			nearest = from;
			for (float step = CLEAR_STEP; step <= 180f; step += CLEAR_STEP)
			{
				// The way the elbow was already headed first, so an even split cannot send it backwards.
				for (int side = 0; side < 2; side++)
				{
					float turn = step * (side == 0 ? toward : -toward);
					if (!ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(turn, axis) * from,
						offset, origin, inverse))
					{
						continue;
					}

					// Onto the boundary itself, so it slides as the body turns rather than stepping.
					float inside = turn - Mathf.Sign(turn) * CLEAR_STEP;
					for (int i = 0; i < CLEAR_BISECTIONS; i++)
					{
						float mid = (inside + turn) * 0.5f;
						if (ClearsBody(capsules, centre, radius, Quaternion.AngleAxis(mid, axis) * from,
							offset, origin, inverse))
						{
							turn = mid;
						}
						else
						{
							inside = mid;
						}
					}

					nearest = Quaternion.AngleAxis(turn, axis) * from;
					return true;
				}
			}

			return false;
		}

		#endregion Body shape

		#region Armaments

		/// <summary>The ID of the <paramref name="index"/>th slot on an arm.</summary>
		public static string SlotID(bool isLeft, int index)
		{
			return $"{(isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND)}_{index}";
		}

		/// <summary>Whether this armament occupies both arms, so the other cannot hold anything of its own.</summary>
		public static bool IsTwoHanded(RuntimeEquipedData data)
		{
			return data != null &&
				data.RuntimeItemData.RuntimeData.TryGetValue(ItemDataIdentifiers.TWO_HANDED, out bool twoHanded) &&
				twoHanded;
		}

		/// <summary>Where the wielding hand grips this armament, or null when its root is the grip.</summary>
		public static Transform GripOf(RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			return carryable == null ? null : carryable.MainHand;
		}

		/// <summary>
		/// Moves <paramref name="root"/> so that <paramref name="grip"/> lands exactly on the target.
		/// Done in world space, so no scale anywhere in either chain can distort it.
		/// </summary>
		public static void AlignGrip(Transform root, Transform grip, Vector3 targetPos, Quaternion targetRot)
		{
			if (grip == null)
			{
				grip = root;
			}

			// Rotate first, then close whatever gap the rotation left.
			root.rotation = targetRot * Quaternion.Inverse(grip.rotation) * root.rotation;
			root.position += targetPos - grip.position;
		}

		/// <summary>
		/// The armament this arm would bring out next: the active one, or — while unarmed — the one it
		/// last held, since that is what re-arming returns to.
		/// </summary>
		public static RuntimeEquipedData NextOut(ArmState arm)
		{
			if (arm.Active != null)
			{
				return arm.Active;
			}

			return arm.LastActiveIndex >= 0 && arm.LastActiveIndex < arm.Armaments.Count
				? arm.Armaments[arm.LastActiveIndex]
				: null;
		}

		/// <summary>
		/// Where this armament sits in its point's stack. The next one out leads, so the body always shows
		/// what the arm will reach for — readable even while unarmed or sheathed.
		/// </summary>
		public static int StowOrder(ArmState arm, RuntimeEquipedData data)
		{
			if (data != null && data == NextOut(arm))
			{
				return 0;
			}

			for (int i = 0; i < arm.Armaments.Count; i++)
			{
				if (arm.Armaments[i] == data)
				{
					return i + 1;
				}
			}
			return int.MaxValue - 1;
		}

		#endregion Armaments
	}
}
