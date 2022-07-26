using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Leg configuration data.
	/// </summary>
	public interface ILeg
	{
		event Action<ILeg, bool> FootstepEvent;

		Transform Thigh { get; }
		Transform Knee { get; }
		Transform Foot { get; }
		float Length { get; }

		bool Grounded { get; }
		float GroundedAmount { get; }
		bool ValidGround { get; }
		Vector3 TargetPoint { get; }
		RaycastHit GroundedHit { get; }
		Vector3 CastOrigin { get; set; }
		Vector3 CastDirection { get; set; }

		float WalkCycleOffset { get; }

		void UpdateFoot(bool grounded, float groundedAmount, bool validGround, Vector3 targetPoint, RaycastHit groundedHit);
	}
}
