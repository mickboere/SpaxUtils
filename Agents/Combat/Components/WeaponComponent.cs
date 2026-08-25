using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Home of a weapon's physical information: where it begins and ends, how far it reaches, and what it
	/// actually delivers when thrust versus swung.
	/// </summary>
	public class WeaponComponent : MonoBehaviour, ICarryableItem
	{
		[field: SerializeField] public Transform Base { get; private set; }
		[field: SerializeField] public Transform Tip { get; private set; }

		[field: SerializeField, Tooltip("Where the wielding hand grips. The root is the sheathe anchor, so this is what gets aligned to the hand.")]
		public Transform MainHand { get; private set; }

		[field: SerializeField, Tooltip("Where a second hand grips, for two-handed use. Optional.")]
		public Transform OffHand { get; private set; }

		/// <inheritdoc/>
		public float CarryRadius => carryRadius;
		/// <inheritdoc/>
		public float WieldRadius => wieldRadius;

		[SerializeField, Min(0f), Tooltip("Radius of the widest part (a hammer head). Drives sheathe clearance and stacking.")]
		private float carryRadius = 0.05f;
		[SerializeField, Min(0f), Tooltip("Radius of the grip. Drives how far off the palm this sits when held, and later how the hand wraps it.")]
		private float wieldRadius = 0.02f;

		[SerializeField, Tooltip("Draw the grips and reach even when this object is not selected.")]
		private bool drawGizmos = true;

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
		/// How far this weapon extends past the hand: <see cref="MainHand"/> to <see cref="Tip"/>.
		/// Falls back to the root for weapons whose root IS the grip (natural weapons, unmigrated prefabs).
		/// </summary>
		public float Reach => overrideReach
			? reachOverride
			: Tip != null ? Vector3.Distance(GripPosition, Tip.position) : 0f;

		/// <summary>Where the wielding hand meets this weapon, in world space.</summary>
		public Vector3 GripPosition => MainHand != null ? MainHand.position : transform.position;

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

		// Drawn unselected so the grips stay visible while the hand transforms themselves are being moved.
		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
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

			// The sheathe anchor the weapon hangs from.
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(transform.position, Vector3.one * 0.01f);

			DrawGrip(MainHand, Color.cyan);
			DrawGrip(OffHand, Color.magenta);
		}

		/// <summary>Grip girth plus an axis cross, since the grip's rotation is what aligns to the hand.</summary>
		private void DrawGrip(Transform grip, Color color)
		{
			if (grip == null)
			{
				return;
			}

			Gizmos.color = color;
			Gizmos.DrawWireSphere(grip.position, wieldRadius);

			float size = wieldRadius * 3f;
			Gizmos.color = Color.red;
			Gizmos.DrawRay(grip.position, grip.right * size);
			Gizmos.color = Color.green;
			Gizmos.DrawRay(grip.position, grip.up * size);
			Gizmos.color = Color.blue;
			Gizmos.DrawRay(grip.position, grip.forward * size);
		}
	}
}
