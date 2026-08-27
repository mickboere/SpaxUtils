using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Physical carry data for anything held or worn on the body: its girths and where the hands grip it.
	/// The root is the sheathe anchor, so an item whose grip is off-centre points <see cref="MainHand"/> at it.
	/// </summary>
	public class CarryableItemComponent : MonoBehaviour, ICarryableItem
	{
		/// <inheritdoc/>
		public float CarryRadius => carryRadius;
		/// <inheritdoc/>
		public float WieldRadius => wieldRadius;
		/// <inheritdoc/>
		public int StackPriority => stackPriority;
		/// <inheritdoc/>
		public string SheatheCategory => sheatheCategory;

		/// <inheritdoc/>
		public Vector3 InsertAxis => Quaternion.Inverse(transform.rotation) *
			(MainHand != null ? MainHand.forward : transform.forward);

		/// <inheritdoc/>
		public virtual float SheathedLength => 0f;

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("Where the wielding hand grips. Leave empty when the root is the grip.")]
		public Transform MainHand { get; private set; }

		/// <inheritdoc/>
		[field: SerializeField, Tooltip("Where a second hand grips, for two-handed use. Optional.")]
		public Transform OffHand { get; private set; }

		[SerializeField, Min(0f), Tooltip("Radius of the widest part. Drives sheathe clearance and stacking.")]
		private float carryRadius = 0.05f;
		[SerializeField, Min(0f), Tooltip("Radius of the grip. Drives how far off the palm this sits when held.")]
		private float wieldRadius = 0.02f;

		[SerializeField, ConstDropdown(typeof(ISheatheCategoryConstants)), Tooltip("How this is carried when not in hand. Decides which sheathe point holds it.")]
		private string sheatheCategory;

		[SerializeField, Tooltip("Overrides sheathe stacking order. Lower rides nearer the top — a shield at -1 sits above the weapons.")]
		private int stackPriority;

		[SerializeField, Tooltip("Draw the grips even when this object is not selected.")]
		private bool drawGizmos = true;

		/// <summary>Whether gizmo drawing is enabled, for subclasses adding their own.</summary>
		protected bool DrawGizmosEnabled => drawGizmos;

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

			DrawGrip(MainHand, Color.cyan);
			DrawGrip(OffHand, Color.magenta);
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
