using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Struct containing basic impact data like source, location and force.
	/// </summary>
	public struct ImpactData
	{
		/// <summary>
		/// The <see cref="IEntity"/> responsible for the impact, if any.
		/// </summary>
		public IEntity Source { get; set; }

		/// <summary>
		/// The <see cref="IEntity"/> that was hit by the impact, if any.
		/// </summary>
		public IEntity Victim { get; set; }

		/// <summary>
		/// The <see cref="GameObject"/> that was hit by the impact, if any.
		/// </summary>
		public GameObject HitObject { get; set; }

		/// <summary>
		/// The impact point in world space.
		/// </summary>
		public Vector3 Location { get; set; }

		/// <summary>
		/// The force of the impact in N.
		/// </summary>
		public float Force { get; set; }

		/// <summary>
		/// The impact direction in world space.
		/// </summary>
		public Vector3 Direction { get; set; }

		/// <summary>
		/// An optional shakesource to override the default shaking behaviour when applied.
		/// </summary>
		public IShakeSource ShakeSource { get; set; }
	}
}
