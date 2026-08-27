using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Home of a weapon's physical information: where it begins and ends, how far it reaches, and what it
	/// actually delivers when thrust versus swung. Carry girths and grips come from the base.
	/// </summary>
	public class WeaponComponent : CarryableItemComponent
	{
		[field: SerializeField] public Transform Base { get; private set; }
		[field: SerializeField] public Transform Tip { get; private set; }

		[SerializeField, ReadOnly, Tooltip("Reach derived from this weapon's geometry (MainHand to Tip). Reference only — refreshed on inspector changes; the live value is read from the transforms.")]
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
		/// How far this weapon extends past the hand: <see cref="ICarryableItem.MainHand"/> to <see cref="Tip"/>.
		/// Falls back to the root for weapons whose root IS the grip (natural weapons, unmigrated prefabs).
		/// </summary>
		public float Reach => overrideReach
			? reachOverride
			: Tip != null ? Vector3.Distance(GripPosition, Tip.position) : 0f;

		/// <summary>Where the wielding hand meets this weapon, in world space.</summary>
		public Vector3 GripPosition => MainHand != null ? MainHand.position : transform.position;

		/// <summary>
		/// The blade, measured along the insert axis — what has to slide clear before the weapon swings free.
		/// </summary>
		public override float SheathedLength => Tip == null
			? 0f
			: Mathf.Abs(Vector3.Dot(Tip.position - (Base != null ? Base.position : GripPosition),
				transform.rotation * InsertAxis));

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
			baseReach = Tip != null ? Vector3.Distance(GripPosition, Tip.position) : 0f;
		}

		protected override void OnDrawGizmos()
		{
			base.OnDrawGizmos();

			if (!DrawGizmosEnabled)
			{
				return;
			}

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
				Gizmos.DrawLine(GripPosition, Tip.position);
			}
		}
	}
}
