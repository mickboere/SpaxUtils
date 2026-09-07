using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Physical carry data for anything held or worn on the body: its girths, its extent and where the
	/// hands grip it. The root is the sheathe anchor, so an item whose grip is off-centre points
	/// <see cref="MainHand"/> at it.
	/// </summary>
	public class CarryableItemComponent : MonoBehaviour, ICarryableItem, IExcludeFromSkeleton
	{
		/// <summary>Held or sheathed, this is never part of the body it hangs on.</summary>
		public bool Exclude => true;

		/// <inheritdoc/>
		public float CarryRadius => carryRadius;
		/// <inheritdoc/>
		public float WieldRadius => wieldRadius;
		/// <inheritdoc/>
		public int StackPriority => stackPriority;
		/// <inheritdoc/>
		public string SheatheCategory => sheatheCategory;

		/// <inheritdoc/>
		public Vector3 InsertAxis => Quaternion.Inverse(transform.rotation) * Grip.forward;

		/// <inheritdoc/>
		public Transform Grip => MainHand != null ? MainHand : transform;

		/// <inheritdoc/>
		public Vector3 GripPosition => Grip.position;

		/// <inheritdoc/>
		public virtual float SheathedLength => 0f;

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("Where the wielding hand grips. Leave empty when the root is the grip.")]
		public Transform MainHand { get; private set; }

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("Where a second hand grips, for two-handed use. Optional.")]
		public Transform OffHand { get; private set; }

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("The butt end — pommel, haft end, bottom rim. Opposite the tip.")]
		public Transform Butt { get; private set; }

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("The far end — a blade's point, a hammer's head, the top rim.")]
		public Transform Tip { get; private set; }

		/// <inheritdoc/>
		public virtual Vector3 CenterOfMass => HasExtent
			? Vector3.Lerp(Butt.position, Tip.position, centerOfMass)
			: GripPosition;

		/// <summary>
		/// The item's own spread about its weight, shifted to the grip it actually turns about.
		/// Falls back to whatever it is drawn as, for anything with no ends marked.
		/// </summary>
		public virtual float GyrationRadius
		{
			get
			{
				if (!HasExtent)
				{
					if (!measuredGyration.HasValue)
					{
						measuredGyration = MeasureGyration();
					}
					return measuredGyration.Value;
				}

				float length = Vector3.Distance(Butt.position, Tip.position);
				float offset = Vector3.Distance(GripPosition, CenterOfMass);
				return Mathf.Max(Mathf.Sqrt(length * length / 12f + offset * offset), carryRadius);
			}
		}

		/// <inheritdoc/>
		public virtual Vector3 AimDirection
		{
			get
			{
				Vector3 line = Tip == null ? Vector3.zero : Tip.position - GripPosition;
				return line.sqrMagnitude < 0.000001f ? Grip.forward : line.normalized;
			}
		}

		/// <inheritdoc/>
		public bool HasExtent => Butt != null && Tip != null;

		/// <inheritdoc/>
		public virtual bool HasAim => false;

		[SerializeField, Min(0f), Tooltip("Radius of the widest part. Drives sheathe clearance and stacking.")]
		private float carryRadius = 0.05f;
		[SerializeField, Min(0f), Tooltip("Radius of the grip. Drives how far off the palm this sits when held.")]
		private float wieldRadius = 0.02f;

		[SerializeField, Range(0f, 1f), Tooltip("Where the weight sits along Butt→Tip. 0.5 is an even rod; lower balances it back into the hand.")]
		private float centerOfMass = 0.5f;

		[SerializeField, ConstDropdown(typeof(ISheatheCategoryConstants)), Tooltip("How this is carried when not in hand. Decides which sheathe point holds it.")]
		private string sheatheCategory;

		[SerializeField, Tooltip("Overrides sheathe stacking order. Lower rides nearer the top — a shield at -1 sits above the weapons.")]
		private int stackPriority;

		[SerializeField, Tooltip("Draw the grips even when this object is not selected.")]
		private bool drawGizmos = true;

		/// <summary>Whether gizmo drawing is enabled, for subclasses adding their own.</summary>
		protected bool DrawGizmosEnabled => drawGizmos;

		// Rigid, so measured once. Cleared wherever the item could have been rebuilt under us.
		private float? measuredGyration;

		protected virtual void OnEnable()
		{
			measuredGyration = null;
		}

		protected virtual void OnValidate()
		{
			measuredGyration = null;
		}

		/// <summary>
		/// The spread of what this item is actually drawn as, plus how far that sits from the grip.
		/// Same form as the authored one, with the box's other two dimensions carrying their share.
		/// </summary>
		private float MeasureGyration()
		{
			Renderer[] renderers = GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
			{
				return carryRadius;
			}

			// Measured in this root's own space, so the result does not depend on how the item is posed.
			Bounds local = default;
			bool started = false;
			foreach (Renderer renderer in renderers)
			{
				Bounds drawn = renderer.localBounds;
				for (int corner = 0; corner < 8; corner++)
				{
					Vector3 sign = new Vector3(
						(corner & 1) == 0 ? -1f : 1f,
						(corner & 2) == 0 ? -1f : 1f,
						(corner & 4) == 0 ? -1f : 1f);
					Vector3 point = transform.InverseTransformPoint(
						renderer.transform.TransformPoint(drawn.center + Vector3.Scale(drawn.extents, sign)));

					if (!started)
					{
						local = new Bounds(point, Vector3.zero);
						started = true;
						continue;
					}
					local.Encapsulate(point);
				}
			}

			Vector3 scale = transform.lossyScale;
			Vector3 size = Vector3.Scale(local.size, scale);
			Vector3 offset = Vector3.Scale(local.center - transform.InverseTransformPoint(GripPosition), scale);
			return Mathf.Max(Mathf.Sqrt(size.sqrMagnitude / 12f + offset.sqrMagnitude), carryRadius);
		}

		// Drawn unselected so the grips stay visible while the hand transforms themselves are being moved.
		protected virtual void OnDrawGizmos()
		{
			if (!drawGizmos)
			{
				return;
			}

			// The sheathe anchor the item hangs from, and the girth it clears the body by.
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(transform.position, Vector3.one * 0.01f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, carryRadius);

			// The main grip always exists, at the root when no transform marks it.
			DrawGrip(Grip, Color.cyan);
			DrawGrip(OffHand, Color.magenta);

			if (HasExtent)
			{
				// The item's line, its weight, and the lever the grip turns it about.
				Gizmos.color = Color.grey;
				Gizmos.DrawLine(Butt.position, Tip.position);
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(CenterOfMass, 0.03f);
				Gizmos.DrawLine(GripPosition, CenterOfMass);
			}
		}

		/// <summary>Grip girth plus an axis cross, since the grip's rotation is what aligns to the hand.</summary>
		protected void DrawGrip(Transform grip, Color color)
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
