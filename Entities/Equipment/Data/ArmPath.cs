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
		/// How hard an armament holds its wielded angle before taking the sheathed one. A cube leaves the
		/// turn in the last quarter of the trip, which is the part nearest the sheathe.
		/// </summary>
		private const float TURN_BIAS = 3f;


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
		/// How far past the body the middle of the arc rides, on top of whatever the body is there.
		/// </summary>
		public float ClearanceOffset;

		/// <summary>The body's own girth by height — what the arc has to get around.</summary>
		public BodyProfile Body;

		/// <summary>Which way round the pose turns: 0 or more the short way, below zero the long way.</summary>
		public float TurnDirection;

		/// <summary>Whether the hand has an armament in it for this leg.</summary>
		public bool Carrying;

		/// <summary>Whether what is carried is going into the sheathe at the end, rather than out at the start.</summary>
		public bool CarriedIn;

		/// <summary>Whether that sheathe sits over the shoulder, where the turn has a wrong way round.</summary>
		public bool TurnsOver;

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
			return (Arc(WithdrawPoint, PreInsertPoint, u), TurnTo(Turn(u), TurnDirection));
		}

		/// <summary>
		/// The pose partway through its turn. Both ways round land in the same place, so which one is
		/// taken is free to be chosen — the short way is only the default, not the right answer.
		/// </summary>
		private Quaternion TurnTo(float t, float direction)
		{
			if (direction >= 0f)
			{
				return Quaternion.Slerp(StartRotation, EndRotation, t);
			}

			Quaternion delta = EndRotation * Quaternion.Inverse(StartRotation);
			delta.ToAngleAxis(out float angle, out Vector3 axis);

			// ToAngleAxis reports 0..360, but Slerp always takes the short arc. Signing the angle the way
			// Slerp reads it is what makes the other way round actually be the other way round.
			float around = angle > 180f ? angle - 360f : angle;
			if (Mathf.Abs(around) < 0.01f)
			{
				return Quaternion.Slerp(StartRotation, EndRotation, t);
			}

			// The rest of the circle: identical at both ends, opposite everywhere between.
			float back = around >= 0f ? around - 360f : around + 360f;
			return Quaternion.AngleAxis(back * t, axis) * StartRotation;
		}

		/// <summary>
		/// Which way round to turn: the one that does not drag what the hand carries through the body.
		/// Settled once when the leg begins — deciding it per frame would let it flip mid-swing.
		/// </summary>
		public float ChooseTurn(out float shortWay, out float longWay)
		{
			shortWay = 0f;
			longWay = 0f;
			if (!Carrying || !TurnsOver)
			{
				return 1f;
			}

			// The slide axis at the sheathe end lies along the armament, so it says where it is pointing.
			Vector3 carried = Quaternion.Inverse(CarriedIn ? EndRotation : StartRotation) *
				(CarriedIn ? EndAxis : StartAxis);
			Vector3 up = Rotation * Vector3.up;

			// Halfway is where the two ways are furthest apart. Over a shoulder the one that lifts the
			// armament goes over it; the other lays it down through the body to get to the same place.
			shortWay = Vector3.Dot(TurnTo(0.5f, 1f) * carried, up);
			longWay = Vector3.Dot(TurnTo(0.5f, -1f) * carried, up);
			return longWay > shortWay ? -1f : 1f;
		}

		/// <summary>
		/// When the pose turns over. A sheathed angle is awkward to carry, so the armament holds its
		/// wielded one and only takes the other up near the sheathe: late going in, early coming out.
		/// </summary>
		private float Turn(float u)
		{
			if (!Carrying)
			{
				return u;
			}

			return CarriedIn ? Mathf.Pow(u, TURN_BIAS) : 1f - Mathf.Pow(1f - u, TURN_BIAS);
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

				// An endpoint authored on the skin measures the body where it sits; one at a sheathe's
				// own grip measures nothing. Whichever is wider is the truer floor.
				Ends = Mathf.Min(radiusA, radiusB),
				Offset = ClearanceOffset,
				Body = Body
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
			public float Ends;
			public float Offset;
			public BodyProfile Body;

			public Vector3 At(float u)
			{
				float angle = Mathf.Lerp(AngleA, AngleB, u) * Mathf.Deg2Rad;
				float window = Mathf.Sin(u * Mathf.PI);
				float height = Mathf.Lerp(HeightA, HeightB, u);

				// The floor is what leaves the body; the window is what still lets both ends sit inside it.
				float radius = Mathf.Lerp(RadiusA, RadiusB, u);
				radius += Mathf.Max(Floor(height) - radius, 0f) * window;

				return new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
			}

			/// <summary>How far out the arc must ride at a height to be clear of the body there.</summary>
			public float Floor(float height)
			{
				return Mathf.Max(Ends, Body.At(height)) + Offset;
			}
		}

		/// <summary>
		/// The body's radius by height, in the torso's frame: pelvis, chest, then only the head, which
		/// is narrow enough that an arc may pass close over the shoulders instead of being flung wide.
		/// </summary>
		public struct BodyProfile
		{
			public float WaistHeight;
			public float WaistRadius;
			public float ChestHeight;
			public float ChestRadius;
			public float HeadHeight;
			public float HeadRadius;

			public float At(float height)
			{
				if (height <= WaistHeight)
				{
					return WaistRadius;
				}
				if (height <= ChestHeight)
				{
					return Mathf.Lerp(WaistRadius, ChestRadius, Span(WaistHeight, ChestHeight, height));
				}
				if (height <= HeadHeight)
				{
					return Mathf.Lerp(ChestRadius, HeadRadius, Span(ChestHeight, HeadHeight, height));
				}

				// Nothing above the crown, so it runs out over the head's own girth.
				return Mathf.Lerp(HeadRadius, 0f, Span(HeadHeight, HeadHeight + HeadRadius * 2f, height));
			}

			public override string ToString()
			{
				return $"waist {WaistRadius:0.###}@{WaistHeight:0.##}" +
					$" chest {ChestRadius:0.###}@{ChestHeight:0.##} head {HeadRadius:0.###}@{HeadHeight:0.##}";
			}

			private static float Span(float from, float to, float at)
			{
				return to - from <= 0.0001f ? 1f : Mathf.Clamp01((at - from) / (to - from));
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
				// The floor where the window pushes hardest, against the body it was taken from.
				$" | floor {orbit.Floor(Mathf.Lerp(a.y, b.y, 0.5f)):0.###} ends {orbit.Ends:0.###} offset {orbit.Offset:0.###}" +
				$" | body {Body}" +
				$" | depth {StartDepth:0.###}/{EndDepth:0.###} | length {Length:0.###}";
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
