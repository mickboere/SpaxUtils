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
		public event Action<ILeg, bool> FootstepEvent;

		public bool Grounded { get; private set; }
		public float GroundedAmount { get; private set; }
		public bool ValidGround { get; private set; }
		public Vector3 TargetPoint { get; private set; }
		public RaycastHit GroundedHit { get; private set; }
		public Vector3 CastOrigin { get; set; }
		public Vector3 CastDirection { get; set; }

		public float MainCycleOffset { get; set; }
		public float WalkCycleOffset { get => walkCycleOffset + MainCycleOffset; }

		[field: SerializeField] public Transform Thigh { get; private set; }
		[field: SerializeField] public Transform Knee { get; private set; }
		[field: SerializeField] public Transform Foot { get; private set; }

		public float Length => (Knee.position - Thigh.position).magnitude + (Foot.position - Knee.position).magnitude;

		[SerializeField, Range(-1f, 1f)] private float walkCycleOffset;

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

		public void UpdateFoot(Vector3 point, bool grounded, float amount)
		{
			throw new NotImplementedException();
		}
	}
}
