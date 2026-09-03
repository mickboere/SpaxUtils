using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// What a limb's middle joint carries between frames: only where it ended up last time, which every
	/// clamp travels from. Held by the caller, so the solver stays usable by any number of limbs at once.
	/// </summary>
	public class ElbowState
	{
		/// <summary>Last frame's direction on the ring, in agent space. A place it is KNOWN to be allowed.</summary>
		public Vector3 Roll;
		public bool HasRoll;
	}

	/// <summary>
	/// Holds a two-bone limb's middle joint to a wanted direction on its solution ring: bounded in how
	/// fast it may travel, kept out of the body, and kept inside the joint's own authored cone.
	/// Every stage travels the ring FROM where the joint already was and stops — none of them picks a
	/// point across it, so the joint can never be teleported to the far side when the nearest legal
	/// pose jumps. What the joint SHOULD want is the caller's to decide.
	/// </summary>
	public class ElbowHintSolver
	{
		/// <summary>Degrees per second the joint may travel round its ring. THE knob for jitter.</summary>
		private const float TURN_RATE = 160f;

		/// <summary>Bisections onto the joint cone's edge once a legal step has been bracketed.</summary>
		private const int LIMIT_BISECTIONS = 8;

		/// <summary>How the cone's edge is searched for: coarse walk out from the joint, then bisection.</summary>
		private const float LIMIT_STEP = 2f;
		private const int RECOVER_BISECTIONS = 6;

		/// <summary>Reach cones found on the limbs' root bones, looked up once. A null entry means none authored.</summary>
		private readonly Dictionary<Transform, RotationLimit> jointLimits = new Dictionary<Transform, RotationLimit>();

		/// <summary>Logs each cone once, the first time it is looked up. Off by default — the caller opts in.</summary>
		public bool DebugLogging;

		/// <summary>The torso as the solver sees it: the shapes to stay out of, and the frame they are in.</summary>
		public struct Torso
		{
			public BodyCapsule[] Capsules;
			public Vector3 Origin;
			public Quaternion Rotation;
		}

		/// <summary>What each stage did, for logging. Debug only — nothing reads it to decide anything.</summary>
		public struct Report
		{
			public Vector3 Centre;
			public float Radius;
			public Vector3 Axis;
			public Vector3 Was;
			public Vector3 Held;
			public float Bodied;
			public float Clamped;
			public string Mode;
		}

		/// <summary>
		/// Where the middle joint should be put this frame, given where the caller wants it.
		/// <paramref name="wanted"/> and the result are directions on the ring, in world space.
		/// </summary>
		public bool TrySolve(ElbowState state, Transform root, Transform joint, Transform tip,
			Vector3 tipTarget, Vector3 wanted, Torso torso, Quaternion agent, float clearance, float delta,
			out Vector3 hint, out Report report)
		{
			hint = Vector3.zero;
			report = default;

			if (root == null || joint == null || wanted.sqrMagnitude < 0.000001f ||
				!LimbHint.TryGetCircle(root, joint, tip, tipTarget,
					out Vector3 centre, out float radius, out Vector3 axis))
			{
				return false;
			}

			// Onto the ring's own plane: a direction off it is not a place the joint could be, and every
			// angle below is measured about the ring's axis.
			Vector3 preferred = Vector3.ProjectOnPlane(wanted, axis);
			if (preferred.sqrMagnitude < 0.000001f)
			{
				return false;
			}
			preferred.Normalize();

			// Last frame's output, which the clamps use as a place the joint is known to have been allowed,
			// and which this frame eases off. It never feeds what is wanted, so neither a clamp nor the
			// damping can write itself into where the joint goes next.
			Vector3 was = state != null && state.HasRoll
				? Vector3.ProjectOnPlane(agent * state.Roll, axis) : Vector3.zero;
			Vector3 reference = was.sqrMagnitude > 0.000001f ? was.normalized : preferred;

			// A joint can only turn so fast. Bounding the TARGET rather than the result keeps the body and
			// the cone the last authority — both travel from the joint and stop, so a short target arc is a
			// short travel. A fraction of the gap instead lurches whenever the gap is large.
			float turn = Vector3.SignedAngle(reference, preferred, axis);
			float allowed = TURN_RATE * delta;
			if (Mathf.Abs(turn) > allowed)
			{
				preferred = Quaternion.AngleAxis(Mathf.Sign(turn) * allowed, axis) * reference;
			}

			// Out of the torso before the cone gets its say, so the cone stays the last authority on what
			// the limb can actually do. Travelled from where the joint was, like the cone's own clamp.
			preferred = ClearOfBody(centre, radius, axis, reference, preferred, torso, clearance,
				out float bodied);

			Vector3 held = HoldWithinLimit(root, joint, reference, preferred, centre, radius, axis,
				torso, out float clamped, out string mode);

			if (state != null)
			{
				state.Roll = Quaternion.Inverse(agent) * held;
				state.HasRoll = true;
			}

			hint = centre + held * radius;
			report = new Report
			{
				Centre = centre,
				Radius = radius,
				Axis = axis,
				Was = reference,
				Held = held,
				Bodied = bodied,
				Clamped = clamped,
				Mode = mode
			};
			return true;
		}

		/// <summary>How far into the body a world-space point sits. Debug only.</summary>
		public static float BodyDepth(Torso torso, Vector3 position)
		{
			Vector3 local = Quaternion.Inverse(torso.Rotation) * (position - torso.Origin);

			float deepest = 0f;
			for (int i = 0; torso.Capsules != null && i < torso.Capsules.Length; i++)
			{
				deepest = Mathf.Max(deepest,
					torso.Capsules[i].Radius - Vector3.Distance(local, torso.Capsules[i].Closest(local)));
			}
			return deepest;
		}

		/// <summary>
		/// Holds the joint out of the body the way the cone holds it inside itself: it travels its ring
		/// from where it was and stops where the body begins. Never picks a point across the ring.
		/// </summary>
		private static Vector3 ClearOfBody(Vector3 centre, float radius, Vector3 axis, Vector3 was,
			Vector3 roll, Torso torso, float offset, out float held)
		{
			held = 0f;
			BodyCapsule[] capsules = torso.Capsules;
			Quaternion inverse = Quaternion.Inverse(torso.Rotation);

			if (capsules == null || capsules.Length == 0 || radius < 0.0001f ||
				ArmUtils.ClearsBody(capsules, centre, radius, roll, offset, torso.Origin, inverse))
			{
				return roll;
			}

			// The margin is what a swing would LIKE. Where the whole ring is inside the body there is no
			// such point, and refusing to move at all would leave the joint further in than it need be.
			if (!ArmUtils.AnyClear(capsules, centre, radius, axis, was, offset, torso.Origin, inverse))
			{
				offset = 0f;
			}

			// Where it came from is inside too — the body has turned out from under it. Back to the nearest
			// clear place, which is close, because it only ever strays a little at a time.
			if (!ArmUtils.ClearsBody(capsules, centre, radius, was, offset, torso.Origin, inverse))
			{
				if (!ArmUtils.TryNearestClear(capsules, centre, radius, axis, was,
					Vector3.SignedAngle(was, roll, axis) >= 0f ? 1f : -1f, offset,
					torso.Origin, inverse, out Vector3 back))
				{
					// Nowhere on this ring is clear at all. Leaving it be beats moving it blind.
					return was;
				}
				was = back;
			}

			// The last clear point on the way there. Both ends move smoothly, so this one does too.
			float sweep = Vector3.SignedAngle(was, roll, axis);
			float from = 0f, to = 1f;
			for (int i = 0; i < ArmUtils.CLEAR_BISECTIONS; i++)
			{
				float mid = (from + to) * 0.5f;
				if (ArmUtils.ClearsBody(capsules, centre, radius,
					Quaternion.AngleAxis(sweep * mid, axis) * was, offset, torso.Origin, inverse))
				{
					from = mid;
				}
				else
				{
					to = mid;
				}
			}

			Vector3 landed = Quaternion.AngleAxis(sweep * from, axis) * was;
			held = Vector3.Angle(roll, landed);
			return landed;
		}

		/// <summary>
		/// The joint held inside its cone by travelling its own ring from where it already was, so it
		/// stops at the edge of what the root allows instead of being teleported to the far side of the
		/// cone when the nearest allowed pose jumps.
		/// </summary>
		private Vector3 HoldWithinLimit(Transform root, Transform joint, Vector3 was, Vector3 roll,
			Vector3 centre, float radius, Vector3 axis, Torso torso, out float clamped, out string mode)
		{
			clamped = 0f;
			mode = "free";
			RotationLimit limit = LimitOn(root, joint, torso);
			if (limit == null || limit.axis == Vector3.zero || root.parent == null ||
				Allows(limit, root, centre + roll * radius))
			{
				return roll;
			}

			// Where it came from is not allowed either — the ring has turned out from under it. Back to the
			// nearest allowed place, which is close, because it only ever strays a little at a time.
			if (!Allows(limit, root, centre + was * radius))
			{
				mode = "RECOVER";
				if (!TryNearestAllowed(limit, root, centre, radius, axis, was,
					Vector3.SignedAngle(was, roll, axis) >= 0f ? 1f : -1f, out Vector3 back))
				{
					// Nowhere on this ring is allowed at all. Leaving it be beats moving it blind.
					return was;
				}
				was = back;
			}
			else
			{
				mode = "edge";
			}

			// The last allowed point on the way there. Both ends move smoothly, so this one does too.
			float sweep = Vector3.SignedAngle(was, roll, axis);
			float from = 0f, to = 1f;
			for (int i = 0; i < LIMIT_BISECTIONS; i++)
			{
				float mid = (from + to) * 0.5f;
				if (Allows(limit, root, centre + (Quaternion.AngleAxis(sweep * mid, axis) * was) * radius))
				{
					from = mid;
				}
				else
				{
					to = mid;
				}
			}

			Vector3 landed = Quaternion.AngleAxis(sweep * from, axis) * was;
			clamped = Vector3.Angle(roll, landed);
			return landed;
		}

		/// <summary>
		/// The nearest direction on the ring the root allows, stepped out from where the joint already is,
		/// so the answer is only ever as far off as the joint actually strayed.
		/// </summary>
		private static bool TryNearestAllowed(RotationLimit limit, Transform root, Vector3 centre,
			float radius, Vector3 axis, Vector3 from, float toward, out Vector3 nearest)
		{
			nearest = from;
			for (float step = LIMIT_STEP; step <= 180f; step += LIMIT_STEP)
			{
				// The way the joint was already headed first, so an even split cannot send it backwards.
				for (int side = 0; side < 2; side++)
				{
					float turn = step * (side == 0 ? toward : -toward);
					if (!Allows(limit, root, centre + (Quaternion.AngleAxis(turn, axis) * from) * radius))
					{
						continue;
					}

					// Onto the boundary itself, so it slides as the ring turns rather than stepping.
					float outside = turn - Mathf.Sign(turn) * LIMIT_STEP;
					for (int i = 0; i < RECOVER_BISECTIONS; i++)
					{
						float mid = (outside + turn) * 0.5f;
						if (Allows(limit, root, centre + (Quaternion.AngleAxis(mid, axis) * from) * radius))
						{
							turn = mid;
						}
						else
						{
							outside = mid;
						}
					}

					nearest = Quaternion.AngleAxis(turn, axis) * from;
					return true;
				}
			}

			return false;
		}

		/// <summary>Whether the root can actually put its joint there.</summary>
		private static bool Allows(RotationLimit limit, Transform root, Vector3 joint)
		{
			Vector3 bone = (joint - root.position).normalized;
			Quaternion aimed = Quaternion.FromToRotation(root.rotation * limit.axis, bone) * root.rotation;
			Quaternion allowed = limit.GetLimitedLocalRotation(
				Quaternion.Inverse(root.parent.rotation) * aimed, out _);

			// By where the axis ends up, not by whether anything changed: a twist limit can change the
			// rotation without moving the joint at all, and only the joint is being asked about.
			return Vector3.Angle(root.parent.rotation * allowed * limit.axis, bone) < 0.5f;
		}

		/// <summary>The root's authored reach cone, if one has been put on it. Looked up once and kept.</summary>
		private RotationLimit LimitOn(Transform root, Transform joint, Torso torso)
		{
			if (jointLimits.TryGetValue(root, out RotationLimit limit))
			{
				return limit;
			}

			limit = root.GetComponent<RotationLimit>();
			jointLimits[root] = limit;

			if (DebugLogging)
			{
				// Where the cone actually ended up: its centre is taken from the bone's local rotation when
				// it wakes, so an animated limb at that moment would tilt it without saying so.
				SpaxDebug.Log("ARMLIMIT", limit == null
					? $"NO RotationLimit on {root.name} — joint runs unclamped."
					// The cone lives in the PARENT's space, so its name is what the limit is actually fixed to.
					: $"{limit.GetType().Name} on {root.name}" +
						$" fixed to {(root.parent == null ? "NOTHING" : root.parent.name)} | axis {limit.axis}" +
						$" measured {(joint == null ? Vector3.zero : Quaternion.Inverse(root.rotation) * (joint.position - root.position).normalized)}" +
						$" | cone centre {(root.parent == null ? Vector3.zero : Quaternion.Inverse(torso.Rotation) * (root.parent.rotation * limit.defaultLocalRotation * limit.axis))}");
			}

			return limit;
		}
	}
}
