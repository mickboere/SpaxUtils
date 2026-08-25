using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Supplies the carry girths for equipment that isn't a weapon — shields, bottles, quick-items.
	/// Weapons get theirs from <see cref="WeaponComponent"/> instead.
	/// </summary>
	public class CarryableItemComponent : MonoBehaviour, ICarryableItem
	{
		/// <inheritdoc/>
		public float CarryRadius => carryRadius;
		/// <inheritdoc/>
		public float WieldRadius => wieldRadius;

		[SerializeField, Min(0f), Tooltip("Radius of the widest part. Drives sheathe clearance and stacking.")]
		private float carryRadius = 0.05f;
		[SerializeField, Min(0f), Tooltip("Radius of the grip. Drives how far off the palm this sits when held.")]
		private float wieldRadius = 0.02f;

		[SerializeField] private bool drawGizmos;

		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
			{
				return;
			}

			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, carryRadius);
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(transform.position, wieldRadius);
		}
	}
}
