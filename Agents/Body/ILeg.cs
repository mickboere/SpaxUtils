using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Leg configuration data.
	/// </summary>
	public interface ILeg
	{
		/// <summary>
		/// Invoked when the leg enters grounded state while previously raised.
		/// </summary>
		event Action<ILeg, bool> FootstepEvent;

		Transform Thigh { get; }
		Transform Knee { get; }
		Transform Foot { get; }

		/// <summary>
		/// The leg's offset from the body's centre.
		/// </summary>
		Vector3 Offset { get; }

		/// <summary>
		/// The total length of this leg.
		/// </summary>
		float Length { get; }

		/// <summary>
		/// Whether the leg is currently grounded.
		/// </summary>
		bool Grounded { get; }

		/// <summary>
		/// The grounded amount, 1 being grounded, 0 being lifted.
		/// </summary>
		float GroundedAmount { get; }

		/// <summary>
		/// Whether the current grounded point is valid (invalid means too great an angle or other factors).
		/// </summary>
		bool ValidGround { get; }

		/// <summary>
		/// The target grounded point of the leg.
		/// </summary>
		Vector3 TargetPoint { get; }

		/// <summary>
		/// The last <see cref="RaycastHit"/> for this leg.
		/// </summary>
		RaycastHit GroundedHit { get; }

		/// <summary>
		/// The raycast origin for the grounded check.
		/// </summary>
		Vector3 CastOrigin { get; set; }

		/// <summary>
		/// The raycast direction for the grounded check.
		/// </summary>
		Vector3 CastDirection { get; set; }

		/// <summary>
		/// The (walking) cycle offset for this specific leg.
		/// </summary>
		float WalkCycleOffset { get; }

		/// <summary>
		/// Overridable elevation value, if set to 0 will return default elevation value.
		/// </summary>
		public float Elevation { get; set; }

		void UpdateFoot(bool grounded, float groundedAmount, bool validGround, Vector3 targetPoint, RaycastHit groundedHit);
	}
}
