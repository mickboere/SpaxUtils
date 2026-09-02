using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// The route a hand takes between two poses: slide straight out of a sheathe, orbit the body, slide
	/// straight into the next. Rebuilt from live endpoints every frame so it tracks a moving body.
	/// </summary>
	public struct ArmPath
	{
		private const int ARC_SAMPLES = 8;

		/// <summary>Steps the swing's turn is integrated over. Fine enough that every step is a few degrees.</summary>
		private const int TURN_SAMPLES = 24;

		/// <summary>Degrees of the swing that must actively FIGHT the short way before the long one wins.</summary>
		private const float OPPOSED_SWING = 24f;

		/// <summary>
		/// How far along toward the apex each end's control sits. Short enough that the ends still lead
		/// where they are aimed, long enough that the curve is round rather than a corner at the middle.
		/// </summary>
		private const float TANGENT_REACH = 0.7f;


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

		/// <summary>
		/// The body's own collision capsules in this frame — what the arc has to get around. Held by
		/// reference to a buffer the component refills, so rebuilding the path every frame costs nothing.
		/// </summary>
		public BodyCapsule[] Body;

		/// <summary>Whether the hand has an armament in it for this leg.</summary>
		public bool Carrying;

		/// <summary>Whether what is carried is going into the sheathe at the end, rather than out at the start.</summary>
		public bool CarriedIn;

		/// <summary>Whether that sheathe sits over the shoulder, where the turn has a wrong way round.</summary>
		public bool TurnsOver;

		/// <summary>
		/// Whether this leg reaches over the shoulder, where the swing comes round the front. Under the
		/// shoulder the front is the chest, and the hand goes round the flank instead.
		/// </summary>
		public bool SwingFront;

		/// <summary>Which way round the flank the swing goes. Settled with the leg, never per frame.</summary>
		public float FlankSide;

		/// <summary>Which way round the leftover roll turns: below zero the long way. Settled with the leg
		/// for the same reason — the end pose is live, so per frame it can flip and reverse the hand.</summary>
		public float Turning;

		/// <summary>Whether this reach actually goes over the shoulder, as opposed to merely coming round
		/// the front. Only one that does may carry the hand up to the shoulder's own height.</summary>
		public bool OverShoulder;

		/// <summary>The shoulder joint in the torso's frame — the thing the swing has to pass under.</summary>
		public Vector3 ShoulderLocal;

		/// <summary>How much of the full semicircle the swing rides, 0 flat to 1 the whole half.</summary>
		public float Bulge;

		/// <summary>How the grip correction is spread across the swing, on top of the arm's own carry.</summary>
		public EasingMethod GripEasing;

		/// <summary>
		/// Which side of the body the swing should ride, decided from the two poses it runs between.
		/// Called once when a leg begins; the answer is then held for the whole of it.
		/// </summary>
		public void SettleArc(bool overShoulder, out bool front, out float side,
			out bool startFar, out bool endFar)
		{
			Quaternion frame = Quaternion.Inverse(Rotation);
			startFar = (frame * (WithdrawPoint - Origin)).x * SideSign < 0f;
			endFar = (frame * (PreInsertPoint - Origin)).x * SideSign < 0f;
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Vector3 chord = b - a;
			Vector3 middle = (a + b) * 0.5f;
			Vector3 flank = Vector3.Cross(chord, Vector3.up);
			Vector3 outward = new Vector3(middle.x, 0f, middle.z);

			// Over the shoulder the hand comes round the front. Under it the front is the chest, so it
			// goes round the flank — unless the reach crosses the body, where there is no flank to take.
			front = overShoulder || flank.sqrMagnitude < 0.000001f || Depth(middle, 0f) > 0f;
			side = Vector3.Dot(flank, outward) >= 0f ? 1f : -1f;
		}

		/// <summary>
		/// Whether each end sits on the far side of the spine from the arm reaching for it. Settled with
		/// the leg, never per frame: a grip near the spine would otherwise flip it, and the arc with it.
		/// </summary>
		public bool StartFar;
		public bool EndFar;

		/// <summary>Which way a dead-behind endpoint is nudged: -1 for the left arm, +1 for the right.</summary>
		public float SideSign;

		/// <summary>Where the hand ends up once it has pulled the armament clear of its resting place.</summary>
		public Vector3 WithdrawPoint => StartPosition - StartAxis * StartDepth;

		/// <summary>Where the hand waits, aligned, before pushing the armament home.</summary>
		public Vector3 PreInsertPoint => EndPosition - EndAxis * EndDepth;

		/// <summary>Distance the hand actually covers, so a leg's duration follows its real length.</summary>
		public float Length => StartDepth + EndDepth + ArcLength();

		/// <summary>
		/// The hand's whole pose at <paramref name="t"/>. A slide reports <see cref="SLIDING"/> and holds
		/// the sheathe's own attitude: it goes straight in or out, so there is no turn in it to speak of.
		/// </summary>
		public (Vector3 pos, Quaternion rot, float arc) Evaluate(float t)
		{
			float u = Place(t, out Vector3 position);
			return u < 0f
				? (position, t < WithdrawFraction ? StartRotation : EndRotation, SLIDING)
				: (position, Turned(u), u);
		}

		/// <summary>
		/// Where the hand is at <paramref name="t"/>, and how far round the swing that is. Separate from
		/// the pose so drawing the route does not pay for a turn it never asks for.
		/// </summary>
		public float Place(float t, out Vector3 position)
		{
			if (t < WithdrawFraction)
			{
				position = Vector3.Lerp(StartPosition, WithdrawPoint, t / WithdrawFraction);
				return SLIDING;
			}

			float insertStart = 1f - InsertFraction;
			if (t > insertStart)
			{
				position = Vector3.Lerp(PreInsertPoint, EndPosition, (t - insertStart) / InsertFraction);
				return SLIDING;
			}

			float span = insertStart - WithdrawFraction;
			float u = span <= 0f ? 1f : Mathf.Clamp01((t - WithdrawFraction) / span);
			position = Arc(WithdrawPoint, PreInsertPoint, u);
			return u;
		}

		/// <summary>
		/// The hand partway round the swing: carried by the PATH, then the one thing the path cannot say
		/// laid on top. Exactly <see cref="StartRotation"/> at 0 and <see cref="EndRotation"/> at 1.
		/// </summary>
		public Quaternion Turned(float u)
		{
			u = Mathf.Clamp01(u);
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Quaternion start = inverse * StartRotation;

			// A path fixes where the hand POINTS and says nothing about the roll about that. What transport
			// lands on against what the grip needs is that roll, and it is fixed by the two ends alone —
			// measured once against the END, never re-measured per frame against a base that is moving.
			Quaternion whole = Swept(a, b, 1f);
			Quaternion owed = Quaternion.Inverse(whole * start) * (inverse * EndRotation);

			return Rotation * Swept(a, b, u) * start * Wound(owed, Turning, u.Ease(GripEasing));
		}

		/// <summary>
		/// Part of the leftover roll, taken the way round that CONTINUES the swing rather than fighting it.
		/// Both ways land in the same place, so the short one is only a default — and at a near half turn
		/// it is a coin toss that can just as easily lay the armament back down through the body.
		/// </summary>
		private static Quaternion Wound(Quaternion owed, float turning, float t)
		{
			float angle = Winding(owed, turning, out Vector3 axis);
			return Mathf.Abs(angle) < 0.01f
				? Quaternion.identity : Quaternion.AngleAxis(angle * t, axis);
		}

		/// <summary>
		/// That roll as a signed angle about an axis, wound the way <see cref="Turning"/> settled on.
		/// Below zero it is taking the long way round.
		/// </summary>
		private static float Winding(Quaternion owed, float turning, out Vector3 axis)
		{
			owed.ToAngleAxis(out float angle, out axis);
			if (angle > 180f)
			{
				angle = 360f - angle;
				axis = -axis;
			}

			return angle < 0.01f ? 0f : (turning < 0f ? angle - 360f : angle);
		}

		/// <summary>
		/// Which way round the leftover roll goes. Called ONCE when the leg begins — the end pose is live,
		/// so deciding this per frame lets it flip mid-swing and the hand reverses on itself.
		/// </summary>
		public float SettleWinding()
		{
			return SettleWinding(out _, out _, out _);
		}

		/// <summary>The same decision with its own terms exposed, so a wrong answer says which term did it.</summary>
		public float SettleWinding(out float dot, out float swing, out float shortWay)
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Quaternion whole = Swept(a, b, 1f);

			Quaternion owed = Quaternion.Inverse(whole * (inverse * StartRotation)) *
				(inverse * EndRotation);
			owed.ToAngleAxis(out float angle, out Vector3 axis);
			if (angle > 180f)
			{
				angle = 360f - angle;
				axis = -axis;
			}

			whole.ToAngleAxis(out swing, out Vector3 sweepAxis);
			if (swing > 180f)
			{
				swing = 360f - swing;
				sweepAxis = -sweepAxis;
			}

			// How much of the swing actually FIGHTS the short way: its turn projected onto the roll's own
			// axis. A sign alone says a path leans the wrong way; this says whether it means it, and a
			// barely-turning path can never reach the bar however opposed it is. An empty hand carries
			// nothing to drag through the body, so it simply takes the shorter route.
			dot = Vector3.Dot(axis, sweepAxis);
			shortWay = angle;
			return Carrying && swing * -dot > OPPOSED_SWING ? -1f : 1f;
		}

		/// <summary>The leftover roll as WOUND, signed. Below zero it took the long way round. Debug only.</summary>
		public float Roll()
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Quaternion whole = Swept(a, b, 1f);

			return Winding(Quaternion.Inverse(whole * (inverse * StartRotation)) *
				(inverse * EndRotation), Turning, out _);
		}

		/// <summary>Where transport alone lands the hand, in the torso's frame. Debug and measurement.</summary>
		public Quaternion Landed()
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			return Rotation * Swept(inverse * (WithdrawPoint - Origin),
				inverse * (PreInsertPoint - Origin), 1f) * (inverse * StartRotation);
		}

		/// <summary>
		/// However far the PATH turns between 0 and <paramref name="u"/>, and nothing else. Integrated
		/// fresh each call, so it carries no history and cannot drift: the same u always gives the same
		/// answer. Every step is between two near-parallel tangents, which is the only place
		/// FromToRotation is well conditioned — across a half turn its axis is arbitrary.
		/// Walked on a FIXED grid and stopped at u, so growing u only ever adds steps to the same prefix.
		/// </summary>
		private Quaternion Swept(Vector3 a, Vector3 b, float u)
		{
			Quaternion swept = Quaternion.identity;
			if (u < 0.0001f)
			{
				return swept;
			}

			Vector3 point = LocalArc(a, b, 0f);
			Vector3 previous = Vector3.zero;
			for (int i = 1; i <= TURN_SAMPLES; i++)
			{
				float at = Mathf.Min(u, i / (float)TURN_SAMPLES);
				Vector3 next = LocalArc(a, b, at);
				Vector3 step = next - point;
				point = next;
				if (step.sqrMagnitude > 0.000000001f)
				{
					step.Normalize();
					if (previous != Vector3.zero)
					{
						swept = Quaternion.FromToRotation(previous, step) * swept;
					}
					previous = step;
				}

				if (at >= u)
				{
					break;
				}
			}

			return swept;
		}

		/// <summary>Reported instead of a swing position when the hand is sliding in or out of a sheathe.</summary>
		public const float SLIDING = -1f;

		/// <summary>The hand's position partway round the swing, for the arm to read its own line off.</summary>
		public Vector3 Swing(float u)
		{
			return Arc(WithdrawPoint, PreInsertPoint, u);
		}

		/// <summary>
		/// The hand swings between the two poses on the circle that has them at opposite ends of it, taking
		/// the half that passes in front of the body. A quadratic through its halfway point, so the arc
		/// still meets each sheathe slide exactly where the slide leaves off.
		/// </summary>
		private Vector3 Arc(Vector3 from, Vector3 to, float u)
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			return Origin + Rotation * LocalArc(inverse * (from - Origin), inverse * (to - Origin), u);
		}

		/// <summary>
		/// The same curve in the torso's own frame. The turn is integrated here rather than in world, so a
		/// body that walks or turns underneath the swing cannot leak into the hand as twist.
		/// </summary>
		private Vector3 LocalArc(Vector3 a, Vector3 b, float u)
		{
			Vector3 chord = b - a;
			Vector3 middle = (a + b) * 0.5f;
			float across = chord.magnitude * 0.5f;
			if (across < 0.0001f)
			{
				return middle;
			}

			// Which way the circle's plane faces is the whole choice of arc, and it is SETTLED when the leg
			// begins. Deciding it here would re-decide it every frame, and the frame it changed its mind
			// the arc would mirror and take the hand straight across the body to get to the other side.
			Vector3 flank = Vector3.Cross(chord, Vector3.up);
			Vector3 outward = new Vector3(middle.x, 0f, middle.z);

			Vector3 bulge = SwingFront || flank.sqrMagnitude < 0.000001f
				? Vector3.ProjectOnPlane(Vector3.forward, chord)
				: flank * FlankSide;

			// Only a chord running dead front to back leaves the front saying nothing, and out from the
			// spine is then the one direction left that is not straight through the body.
			if (bulge.sqrMagnitude < 0.000001f)
			{
				bulge = Vector3.ProjectOnPlane(outward.sqrMagnitude > 0.000001f
					? outward.normalized : Vector3.right, chord);
				if (bulge.sqrMagnitude < 0.000001f)
				{
					return Vector3.Lerp(a, b, u);
				}
			}

			// Each end gets its OWN control. Aiming both at one apex is what a single control already did,
			// and it cannot describe a route that leaves round the side and arrives round the front.
			Vector3 first, second;
			if (OverShoulder)
			{
				Vector3 apex = Apex(middle, bulge.normalized, across);
				first = Clear(Vector3.Lerp(a, apex, TANGENT_REACH), ClearanceOffset);
				second = Clear(Vector3.Lerp(b, apex, TANGENT_REACH), ClearanceOffset);
			}
			else
			{
				first = Guide(a, StartFar);
				second = Guide(b, EndFar);
			}

			float inverseU = 1f - u;
			Vector3 local = a * (inverseU * inverseU * inverseU) +
				first * (3f * inverseU * inverseU * u) +
				second * (3f * inverseU * u * u) +
				b * (u * u * u);

			// The body only ever pushes the arc further out, so it can never undo the way round it took.
			// Windowed, so both ends still land exactly where the slides put them.
			return Vector3.Lerp(local, Clear(local, Margin(a, b)), Mathf.Sin(u * Mathf.PI));
		}

		/// <summary>
		/// The one point the whole swing is shaped by, PLACED rather than derived. A semicircle on the
		/// chord puts it at a fixed distance square to the chord, which knows nothing of where the body or
		/// the shoulder are — so no choice of direction could stop it cutting through them.
		/// </summary>
		private Vector3 Apex(Vector3 middle, Vector3 bulge, float across)
		{
			if (OverShoulder)
			{
				return Clear(middle + bulge * (across * Bulge), ClearanceOffset);
			}

			// Under the shoulder the swing rides where a hanging arm already is: out beneath the joint, at
			// the height the two ends average to, so a grip drawn clear of its sheathe is never dragged down.
			Vector3 apex = Vector3.Lerp(middle, new Vector3(ShoulderLocal.x, middle.y, ShoulderLocal.z), Bulge);
			apex.y = Mathf.Min(apex.y, ShoulderLocal.y - ClearanceOffset);
			return Clear(apex, ClearanceOffset);
		}

		/// <summary>
		/// Where the hand passes on its way out of or in to one end: out under the shoulder, at that end's
		/// own height. An end on the far side of the body is come at from the FRONT, the only way round.
		/// </summary>
		private Vector3 Guide(Vector3 end, bool far)
		{
			Vector3 guide = new Vector3(ShoulderLocal.x,
				Mathf.Min(end.y, ShoulderLocal.y - ClearanceOffset), ShoulderLocal.z);
			if (far)
			{
				guide.z = Mathf.Max(guide.z, Front(guide, ClearanceOffset));
			}

			return Clear(Vector3.Lerp(end, guide, TANGENT_REACH), ClearanceOffset);
		}

		/// <summary>
		/// How far forward the body reaches at <paramref name="at"/>'s own height and side, plus a margin.
		/// The exact front of each capsule's sphere there, so a narrow waist is not treated like a chest.
		/// </summary>
		private float Front(Vector3 at, float offset)
		{
			float front = at.z;
			for (int i = 0; Body != null && i < Body.Length; i++)
			{
				Vector3 axis = Body[i].Closest(new Vector3(at.x, at.y, 0f));
				float needed = Body[i].Radius + offset;
				float across = needed * needed -
					(at.x - axis.x) * (at.x - axis.x) - (at.y - axis.y) * (at.y - axis.y);
				if (across > 0f)
				{
					front = Mathf.Max(front, axis.z + Mathf.Sqrt(across));
				}
			}
			return front;
		}

		/// <summary>
		/// The clearance the swing may ask for, less whatever the ends themselves already give up. A grip
		/// authored inside a capsule would otherwise have the whole arc shoved out around it.
		/// </summary>
		private float Margin(Vector3 a, Vector3 b)
		{
			return Mathf.Max(0f, ClearanceOffset - Mathf.Max(Depth(a, ClearanceOffset), Depth(b, ClearanceOffset)));
		}

		/// <summary>
		/// <paramref name="local"/> pushed out of every capsule it is inside, by <paramref name="offset"/>
		/// past each. Unchanged when it is already clear of all of them.
		/// Every capsule contributes and none is elected: a winner would swap partway along the arc and
		/// take the push to a different surface with it, which is a CORNER in an otherwise smooth curve.
		/// Each term instead fades to nothing at its own boundary, so the total cannot jump.
		/// </summary>
		private Vector3 Clear(Vector3 local, float offset)
		{
			if (Body == null)
			{
				return local;
			}

			Vector3 push = Vector3.zero;
			for (int i = 0; i < Body.Length; i++)
			{
				Vector3 closest = Body[i].Closest(local);
				Vector3 away = local - closest;
				float distance = away.magnitude;
				float depth = Body[i].Radius + offset - distance;
				if (depth <= 0f)
				{
					continue;
				}

				// Straight down a capsule's own axis there is no "out", so the arm's side of the body is
				// the only direction left that is not further through it.
				push += (distance > 0.0001f ? away / distance : Sideways(closest)) * depth;
			}

			return local + push;
		}

		/// <summary>How far inside the body <paramref name="local"/> is, counting <paramref name="offset"/> as body.</summary>
		private float Depth(Vector3 local, float offset)
		{
			float deepest = 0f;
			for (int i = 0; Body != null && i < Body.Length; i++)
			{
				deepest = Mathf.Max(deepest,
					Body[i].Radius + offset - Vector3.Distance(local, Body[i].Closest(local)));
			}
			return deepest;
		}

		/// <summary>Out from the spine on the arm's own side — the fallback when a point has no way out.</summary>
		private Vector3 Sideways(Vector3 from)
		{
			Vector3 out2D = new Vector3(from.x, 0f, from.z);
			return out2D.sqrMagnitude > 0.000001f ? out2D.normalized : new Vector3(SideSign, 0f, 0f);
		}

		/// <summary>
		/// One of the body's own collision capsules, reduced to the segment between its end-sphere centres
		/// and a radius, in the torso's frame. Authored on the rig, so it is the shape actually seen.
		/// </summary>
		public struct BodyCapsule
		{
			public Vector3 Start;
			public Vector3 End;
			public float Radius;

			/// <summary>The point on the capsule's axis nearest <paramref name="local"/>.</summary>
			public Vector3 Closest(Vector3 local)
			{
				Vector3 along = End - Start;
				float length = along.sqrMagnitude;
				if (length < 0.000001f)
				{
					return Start;
				}

				return Start + along * Mathf.Clamp01(Vector3.Dot(local - Start, along) / length);
			}

			public override string ToString()
			{
				return $"{Start}-{End} r {Radius:0.###}";
			}
		}

		/// <summary>How this path is being shaped, for logging. Debug only.</summary>
		public string Describe()
		{
			Quaternion inverse = Quaternion.Inverse(Rotation);
			Vector3 a = inverse * (WithdrawPoint - Origin);
			Vector3 b = inverse * (PreInsertPoint - Origin);
			Vector3 apex = inverse * (Arc(WithdrawPoint, PreInsertPoint, 0.5f) - Origin);

			return $"side {SideSign:0} {(SwingFront ? "front" : "flank")}" +
				// Which end is on the far side of the spine, and so must be come at round the front.
				$" far {(StartFar ? "A" : "-")}{(EndFar ? "B" : "-")}" +
				$" bulge {Bulge:0.##} | localA {a} localB {b}" +
				// The circle the swing rides: the two ends are opposite on it, so this is its whole size.
				$" | across {Vector3.Distance(a, b) * 0.5f:0.###}" +
				// Halfway round, which is where the arc either comes past the front or does not.
				$" | apex {apex} forward {apex.z:0.###} out {new Vector2(apex.x, apex.z).magnitude:0.###}" +
				// How far the apex still sits inside the body, and how much of the offset the ends allow.
				$" | inside {Depth(apex, 0f):0.###} margin {Margin(a, b):0.###} offset {ClearanceOffset:0.###}" +
				// Under the shoulder the apex is placed beneath the joint; over it, out along the chord.
				$" | shoulder {ShoulderLocal} {(OverShoulder ? "over" : "under")}" +
				// How far apart the two grips are before the arm has carried the hand anywhere. The ARM
				// log reports what is actually left for the wrist once the carry has had its say.
				$" | grips {Quaternion.Angle(StartRotation, EndRotation):0.#}" +
				$" | body {Describe(Body)}" +
				$" | depth {StartDepth:0.###}/{EndDepth:0.###} | length {Length:0.###}";
		}

		/// <summary>The capsules the arc is being kept out of, for logging. Debug only.</summary>
		private static string Describe(BodyCapsule[] body)
		{
			if (body == null || body.Length == 0)
			{
				return "NONE";
			}

			string text = $"{body.Length}:";
			for (int i = 0; i < body.Length; i++)
			{
				text += $" [{body[i]}]";
			}
			return text;
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
