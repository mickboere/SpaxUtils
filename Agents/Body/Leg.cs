using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Leg configuration data class implementing <see cref="ILeg"/>.
	/// </summary>
	[Serializable]
	public class Leg : ILeg
	{
		/// <inheritdoc/>
		public event Action<ILeg, bool> FootstepEvent;

		/// <inheritdoc/>
		public bool Grounded { get; private set; }

		/// <inheritdoc/>
		public float GroundedAmount { get; private set; }

		/// <inheritdoc/>
		public bool ValidGround { get; private set; }

		/// <inheritdoc/>
		public Vector3 TargetPoint { get; private set; }

		/// <inheritdoc/>
		public RaycastHit GroundedHit { get; private set; }

		/// <inheritdoc/>
		public Vector3 CastOrigin { get; set; }

		/// <inheritdoc/>
		public Vector3 CastDirection { get; set; }

		/// <summary>
		/// The global (walking) cycle offset.
		/// </summary>
		public float MainCycleOffset { get; set; }

		/// <inheritdoc/>
		public float Elevation { get { return elevationOverride.Approx(0f) ? elevation : elevationOverride; } set { elevationOverride = value; } }

		public float WalkCycleOffset { get => walkCycleOffset + MainCycleOffset; }

		[field: SerializeField] public Transform Thigh { get; private set; }
		[field: SerializeField] public Transform Knee { get; private set; }
		[field: SerializeField] public Transform Foot { get; private set; }

		public float Length => (Knee.position - Thigh.position).magnitude + (Foot.position - Knee.position).magnitude;

		

		[SerializeField, Range(-1f, 1f)] private float walkCycleOffset;
		[SerializeField] private float elevation;

		private float elevationOverride;

		public void UpdateFoot(bool grounded, float groundedAmount, bool validGround, Vector3 groundedPoint, RaycastHit groundedHit)
		{
			GroundedAmount = groundedAmount;
			ValidGround = validGround;
			TargetPoint = groundedPoint;
			GroundedHit = groundedHit;

			// Footsteps
			if (grounded != Grounded)
			{
				Grounded = grounded;
				FootstepEvent?.Invoke(this, grounded);
			}
		}
	}
}
