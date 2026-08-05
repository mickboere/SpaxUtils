using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Home of a weapon's physical information: where it begins and ends, how far it reaches, and what it
	/// actually delivers when thrust versus swung.
	/// </summary>
	public class WeaponComponent : MonoBehaviour
	{
		[field: SerializeField] public Transform Base { get; private set; }
		[field: SerializeField] public Transform Tip { get; private set; }

		[SerializeField, ReadOnly, Tooltip("The reach derived from this weapon's geometry (root to Tip). Reference only — refreshed on inspector changes; the live value is always read from the transforms.")]
		private float baseReach;

		[SerializeField, Tooltip("Ignore the geometry and use a hand-set reach instead. Only for weapons whose tip does not correspond to how far they actually reach.")]
		private bool overrideReach;
		[SerializeField, Conditional(nameof(overrideReach)), Min(0f)] private float reachOverride;

		// How much of the weapon's physic distribution is REALIZED in each usage — multipliers, never
		// replacements: the distribution stays authoritative for how much slash/power/pierce the weapon has.
		// All default to 1 so an unconfigured weapon simply passes its distribution through untouched.
		// POWER has no usage lane: a weapon's power is its heaviness, and heaviness is behind every angle. A
		// weaker weapon lowers its POWER physic in its stats instead of hiding it in a per-angle slider.
		[Header("Thrust")]
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of the weapon's SLASH realized when thrust.")] private float thrustSlash = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of the weapon's PIERCE realized when thrust.")] private float thrustPierce = 1f;

		[Header("Swing")]
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of the weapon's SLASH realized when swung.")] private float swingSlash = 1f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of the weapon's PIERCE realized when swung.")] private float swingPierce = 1f;

		/// <summary>
		/// How far this weapon extends past the hand: ROOT to <see cref="Tip"/>. The root sits exactly where the
		/// hand grips, while <see cref="Base"/> sits forward of it — measuring Base to Tip would undercount by
		/// the length of the grip.
		/// </summary>
		public float Reach => overrideReach
			? reachOverride
			: Tip != null ? Vector3.Distance(transform.position, Tip.position) : 0f;

		/// <summary>
		/// What this weapon realizes when driven point-first (x=Slash, y=Power, z=Pierce). A sword's tip pierces;
		/// a warpick's spike faces sideways so a thrust is all haft, and lands blunt. Power is always fully
		/// realized — it is the weapon's heaviness, which no angle can take away.
		/// </summary>
		public Vector3 ThrustProfile => new Vector3(thrustSlash, 1f, thrustPierce);

		/// <summary>
		/// What this weapon realizes when swung (x=Slash, y=Power, z=Pierce). Not an either/or with
		/// <see cref="ThrustProfile"/> — a spiked club rakes, bludgeons and punctures all at once.
		/// </summary>
		public Vector3 SwingProfile => new Vector3(swingSlash, 1f, swingPierce);

		protected void OnValidate()
		{
			baseReach = Tip != null ? Vector3.Distance(transform.position, Tip.position) : 0f;
		}

		protected void OnDrawGizmosSelected()
		{
			if (Base != null && Tip != null)
			{
				// The blade.
				Gizmos.color = Color.red;
				Gizmos.DrawLine(Base.position, Tip.position);
			}

			if (Tip != null)
			{
				// The reach, from the grip.
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(transform.position, Tip.position);
			}
		}
	}
}
