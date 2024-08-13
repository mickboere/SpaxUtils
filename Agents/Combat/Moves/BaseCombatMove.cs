using UnityEngine;

namespace SpaxUtils
{
	public abstract class BaseCombatMove : PerformanceMove, ICombatMove
	{
		/// <inheritdoc/>
		public float Range => range;

		[Header("COMBAT")]
		[SerializeField] private float range;
	}

}
