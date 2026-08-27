using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The route a hand takes between two poses: slide straight out of a sheathe, orbit the body, slide
	/// straight into the next. Rebuilt from live endpoints every frame so it tracks a moving body.
	/// </summary>
	public struct ArmPath
	{
		/// <summary>Beyond this far from front the arc would cross the back, so the endpoint is nudged aside.</summary>
		private const float BEHIND_ANGLE = 150f;

		private const int ARC_SAMPLES = 8;

		/// <summary>
		/// The frame the orbit is described in — the torso's, not the agent root's, since the body twists
		/// freely against the root. Rigid, so no scale anywhere can distort it.
		/// </summary>
		public Vector3 Origin;
		public Quaternion Rotation;

		public Vector3 StartPosition;
		public Quaternion StartRotation;
		public Vector3 EndPosition;
		public Quaternion EndRotation;

		/// <summary>World direction the armament slides into its resting place, and how deep, at each end.</summary>
		public Vector3 StartAxis;
		public float StartDepth;
		public Vector3 EndAxis;
		public float EndDepth;

		/// <summary>Share of the leg's time spent sliding, fixed when the leg begins.</summary>
		public float WithdrawFraction;
		public float InsertFraction;

		/// <summary>
		/// How far past the body the middle of the arc rides. The body's own radius comes from the
		/// endpoints, which are authored flush with the skin.
		/// </summary>
		public float ClearanceOffset;

		/// <summary>Which way a dead-behind endpoint is nudged: -1 for the left arm, +1 for the right.</summary>
		public float SideSign;

		/// <summary>Where the hand ends up once it has pulled the armament clear of its resting place.</summary>
		public Vector3 WithdrawPoint => StartPosition - StartAxis * StartDepth;

		/// <summary>Where the hand waits, aligned, before pushing the armament home.</summary>
		public Vector3 PreInsertPoint => EndPosition - EndAxis * EndDepth;

		/// <summary>Distance the hand actually covers, so a leg's duration follows its real length.</summary>
		public float Length => StartDepth + EndDepth + ArcLength();

		/// <summary>
		/// The hand pose at <paramref name="t"/>. Rotation completes across the arc, so the armament is
		/// already aligned by the time it reaches the sheathe and the insert is pure translation.
		/// </summary>
		public (Vector3 pos, Quaternion rot) Evaluate(float t)
		{
			if (t < WithdrawFraction)
			{
				return (Vector3.Lerp(StartPosition, WithdrawPoint, t / WithdrawFraction), StartRotation);
			}

			float insertStart = 1f - InsertFraction;
			if (t > insertStart)
			{
				return (Vector3.Lerp(PreInsertPoint, EndPosition, (t - insertStart) / InsertFraction), EndRotation);
			}

			float span = insertStart - WithdrawFraction;
			float u = span <= 0f ? 1f : Mathf.Clamp01((t - WithdrawFraction) / span);
			return (Arc(WithdrawPoint, PreInsertPoint, u), Quaternion.Slerp(StartRotation, EndRotation, u));
		}

		/// <summary>
		/// Orbits the body instead of cutting across it: angle, radius and height are interpolated
		/// separately, so the path can never collapse toward the spine or swing round the back.
		/// </summary>
		private Vector3 Arc(Vector3 from, Vector3 to, float u)
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (from - Origin);
			Vector3 b = inverse * (to - Origin);

			Orbit orbit = Shape(a, b);
			Vector3 local = orbit.At(u);

			// Insurance only: with true bearings the ends already reconstruct exactly, so this is zero.
			// It stays because anything that ever reshapes the orbit must not move where the hand lands.
			local += (a - orbit.At(0f)) * (1f - u) + (b - orbit.At(1f)) * u;

			return Origin + Rotation * local;
		}

		/// <summary>Describes the orbit between two points in the torso's frame.</summary>
		private Orbit Shape(Vector3 a, Vector3 b)
		{
			// Both angles live in (-180,180], so interpolating them raw can never cross the back.
			(float angleA, float angleB) = Bearings(a, b);
			float radiusA = new Vector2(a.x, a.z).magnitude;
			float radiusB = new Vector2(b.x, b.z).magnitude;

			return new Orbit
			{
				AngleA = angleA,
				AngleB = angleB,
				RadiusA = radiusA,
				RadiusB = radiusB,
				HeightA = a.y,
				HeightB = b.y,

				// The anchors sit on the skin, so the tighter of them is the body's own radius here.
				Clearance = Mathf.Min(radiusA, radiusB) + ClearanceOffset
			};
		}

		/// <summary>An orbit around the torso's upright axis, in its own frame.</summary>
		private struct Orbit
		{
			public float AngleA;
			public float AngleB;
			public float RadiusA;
			public float RadiusB;
			public float HeightA;
			public float HeightB;
			public float Clearance;

			public Vector3 At(float u)
			{
				float angle = Mathf.Lerp(AngleA, AngleB, u) * Mathf.Deg2Rad;
				float window = Mathf.Sin(u * Mathf.PI);

				// The floor is what leaves the body; the window is what still lets both ends sit inside it.
				float radius = Mathf.Lerp(RadiusA, RadiusB, u);
				radius += Mathf.Max(Clearance - radius, 0f) * window;
				float height = Mathf.Lerp(HeightA, HeightB, u);

				return new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
			}
		}

		/// <summary>Both endpoints' true bearings, with only dead-behind's ambiguity settled.</summary>
		private (float a, float b) Bearings(Vector3 localA, Vector3 localB)
		{
			return (Settle(Mathf.Atan2(localA.x, localA.z) * Mathf.Rad2Deg),
				Settle(Mathf.Atan2(localB.x, localB.z) * Mathf.Rad2Deg));
		}

		/// <summary>
		/// Dead behind is the same place approached from either side, so it comes round the arm's own.
		/// Every other bearing is kept exactly: interpolating true bearings in (-180,180] passes through
		/// the front by construction, and a hand crossing the body in front is a cross-draw, not a fault.
		/// </summary>
		private float Settle(float angle)
		{
			return Mathf.Abs(angle) > BEHIND_ANGLE ? SideSign * Mathf.Abs(angle) : angle;
		}

		/// <summary>How this path is being shaped, for logging. Debug only.</summary>
		public string Describe()
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Orbit orbit = Shape(a, b);

			return $"side {SideSign:0} | localA {a} localB {b}" +
				$" | raw {Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg:0.#} -> {Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg:0.#}" +
				$" | held {orbit.AngleA:0.#} -> {orbit.AngleB:0.#}" +
				$" | radius {orbit.RadiusA:0.###} -> {orbit.RadiusB:0.###}" +
				$" | clearance {orbit.Clearance:0.###} | depth {StartDepth:0.###}/{EndDepth:0.###} | length {Length:0.###}";
		}

		private float ArcLength()
		{
			Vector3 from = WithdrawPoint;
			Vector3 to = PreInsertPoint;

			float length = 0f;
			Vector3 previous = from;
			for (int i = 1; i <= ARC_SAMPLES; i++)
			{
				Vector3 point = Arc(from, to, i / (float)ARC_SAMPLES);
				length += Vector3.Distance(previous, point);
				previous = point;
			}
			return length;
		}
	}
}
