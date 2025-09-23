using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// (Humanoid) Leg configuration data.
	/// </summary>
	[Serializable]
	public class Leg
	{
		/// <summary>
		/// Invoked when the leg enters grounded state while previously raised.
		/// </summary>
		public event Action<Leg, bool> FootstepEvent;

		/// <summary>
		/// Whether the leg is currently grounded.
		/// </summary>
		public bool Grounded { get; private set; }

		/// <summary>
		/// The grounded amount, 1 being grounded, 0 being lifted.
		/// </summary>
		public float GroundedAmount { get; private set; }

		/// <summary>
		/// Whether the current grounded point is valid (invalid means too great an angle or other factors).
		/// </summary>
		public bool ValidGround { get; private set; }

		/// <summary>
		/// The target grounded point of the leg.
		/// </summary>
		public Vector3 TargetPoint { get; private set; }

		/// <summary>
		/// The last <see cref="RaycastHit"/> for this leg.
		/// </summary>
		public RaycastHit GroundedHit { get; private set; }

		/// <summary>
		/// The raycast origin for the grounded check.
		/// </summary>
		public Vector3 CastOrigin { get; set; }

		/// <summary>
		/// The raycast direction for the grounded check.
		/// </summary>
		public Vector3 CastDirection { get; set; }

		/// <summary>
		/// The (walking) cycle offset for this specific leg.
		/// </summary>
		public float WalkCycleOffset { get => walkCycleOffset + MainCycleOffset; }

		/// <summary>
		/// The global (walking) cycle offset.
		/// </summary>
		public float MainCycleOffset { get; set; }

		/// <summary>
		/// Overridable elevation value, if set to 0 will return default elevation value.
		/// </summary>
		public float Elevation { get { return elevationOverride.Approx(0f) ? elevation : elevationOverride; } set { elevationOverride = value; } }

		/// <summary>
		/// The total length of this leg.
		/// </summary>
		public float Length => (KneePos - ThighPos).magnitude + (FootPos - KneePos).magnitude;

		[field: SerializeField] public Transform Thigh { get; private set; }
		public Vector3 ThighPos { get; private set; }
		[field: SerializeField] public Transform Knee { get; private set; }
		public Vector3 KneePos { get; private set; }
		[field: SerializeField] public Transform Foot { get; private set; }
		public Vector3 FootPos { get; private set; }
		/// <summary>
		/// Helper transform placed at the bottom of the foot, facing towards the toes.
		/// </summary>
		[field: SerializeField] public Transform Sole { get; private set; }
		public Vector3 SolePos { get; private set; }

		[SerializeField, Range(-1f, 1f)] private float walkCycleOffset;
		[SerializeField] private float elevation;

		private float elevationOverride;

		/// <summary>
		/// Update the foot physics data.
		/// </summary>
		public void UpdateFoot(bool grounded, float groundedAmount, bool validGround, Vector3 targetPoint, RaycastHit groundedHit)
		{
			GroundedAmount = groundedAmount;
			ValidGround = validGround;
			TargetPoint = targetPoint;
			GroundedHit = groundedHit;

			// Footsteps
			if (grounded != Grounded)
			{
				Grounded = grounded;
				FootstepEvent?.Invoke(this, grounded);
			}
		}

		/// <summary>
		/// Called in LateUpdate, keeps track of animated leg bone positions so that FixedUpdate can safely and accurately retrieve them.
		/// </summary>
		public void UpdatePositions()
		{
			ThighPos = Thigh.position;
			KneePos = Knee.position;
			FootPos = Foot.position;
			SolePos = Sole.position;
		}
	}
}
