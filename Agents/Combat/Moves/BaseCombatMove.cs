using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Abstract <see cref="ICombatMove"/> implementation.
	/// </summary>
	public abstract class BaseCombatMove : PerformanceMove, ICombatMove
	{
		/// <inheritdoc/>
		public float Range => range;

		[Header("COMBAT")]
		[SerializeField] private float range;
	}
}
