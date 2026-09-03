using UnityEngine;

namespace SpaxUtils
{
	/// <summary>One arm's journey: put away what it holds, pick up what it should, come home.</summary>
	public class ArmTransition
	{
		/// <summary>Seconds a leg spends handing the arm over, however long the leg itself is.</summary>
		private const float HANDOVER_TIME = 0.1f;

		public ArmState Arm;
		public RuntimeEquipedData Stowing;
		public RuntimeEquipedData Drawing;

		/// <summary>
		/// How <see cref="Drawing"/> slides clear of its OWN sheathe, captured before the transition
		/// touches anything. By the time Recover needs it, <see cref="Drawing"/> has already been
		/// pulled from the sheathe registry and reoriented to the grip, so asking then reads that
		/// instead — the withdraw has to be measured while it is still true.
		/// </summary>
		public Vector3 WithdrawAxis;
		public float WithdrawDepth;

		public TransitionLeg Leg;
		public float Time;
		public float Duration;
		public LegBlend Blend;

		/// <summary>Endpoints of the current leg. Null start means the frozen pose, null end means home.</summary>
		public RuntimeEquipedData From;
		public RuntimeEquipedData To;

		/// <summary>The straight slide out of and into a sheathe, in agent space.</summary>
		public Vector3 StartAxis;
		public float StartDepth;
		public Vector3 EndAxis;
		public float EndDepth;

		/// <summary>Which ends took the over-shoulder way in rather than an armament's own slide. Debug only.</summary>
		public bool StartGated;
		public bool EndGated;

		/// <summary>Which side of the body the swing rides, settled with the leg: re-deciding it mid-swing
		/// mirrors the arc and throws the hand across the body.</summary>
		public bool ArcFront;
		public float ArcSide;

		/// <summary>Which way round the leftover roll turns: below zero the long way. Settled too, or
		/// a live end pose flips it mid-swing and the hand reverses on itself.</summary>
		public float Turning;

		/// <summary>Whether each end sits on the far side of the spine from the arm reaching for it.</summary>
		public bool StartFar;
		public bool EndFar;

		/// <summary>Whether either end of this leg is over the shoulder. The swing may only rise to
		/// shoulder height when it is, and passes under the joint when it is not.</summary>
		public bool ReachesOver;

		/// <summary>What the hand holds on this leg: whether it holds anything, which way that is
		/// headed, and whether its sheathe is over the shoulder.</summary>
		public bool Carrying;
		public bool CarriedIn;
		public bool TurnsOver;

		/// <summary>The animation's elbow direction and the arm line it was sampled on, both PINNED when
		/// the swap latched, and that pair turned onto the line the arm has now. Agent space.</summary>
		public Vector3 SwivelSeed;
		public Vector3 SwivelAxis;
		public Vector3 SwivelRoll;
		public bool HasSwivel;

		/// <summary>What the elbow solver carries between frames for this leg. Held here rather than by
		/// the solver so the solver stays usable by any number of limbs at once.</summary>
		public readonly ElbowState Elbow = new ElbowState();

		/// <summary>Share of this leg's time spent sliding rather than swinging.</summary>
		public float WithdrawFraction;
		public float InsertFraction;

		/// <summary>How far past the skin the middle of this leg's arc rides.</summary>
		public float Clearance;

		/// <summary>IK influence applied this frame.</summary>
		public float Weight;

		/// <summary>How far out on a limb this leg has the arm. Movement control follows it.</summary>
		public float Commitment;

		/// <summary>Hand-over point, in agent space so it follows the body.</summary>
		public Vector3 FrozenPosition;
		public Quaternion FrozenRotation;

		/// <summary>Where the animation had the hand when the arm was claimed — what recovery returns to.</summary>
		public Vector3 HomePosition;
		public Quaternion HomeRotation;

		/// <summary>The torso's twist against the root when the arm was claimed. Debug only.</summary>
		public Quaternion HomeTwist;

		/// <summary>
		/// How much of the hand this leg owns at <paramref name="t"/>. The arc is only meaningful at
		/// full weight, so a leg taking over from animation ramps in quickly; one handing back follows
		/// the hand home instead, and so is spent by the time it arrives.
		/// </summary>
		public float WeightAt(float t, float authority)
		{
			// A fixed handover however long the leg is — it takes what it takes to not pop.
			float blend = Duration <= 0f ? 1f : Mathf.Clamp01(HANDOVER_TIME / Duration);

			switch (Blend)
			{
				case LegBlend.In:
					return Mathf.Clamp01(t / blend);
				case LegBlend.Out:
					return Mathf.Min(authority, Mathf.Clamp01((1f - t) / blend));
				default:
					return 1f;
			}
		}

		/// <summary>
		/// How far out on a limb this leg has the arm at <paramref name="t"/>. Separate from IK weight,
		/// which commits almost immediately and would otherwise snap movement control down with it.
		/// </summary>
		public float CommitmentAt(float t)
		{
			switch (Blend)
			{
				case LegBlend.In:
					return t;
				case LegBlend.Out:
					return 1f - t;
				default:
					return 1f;
			}
		}
	}
}
