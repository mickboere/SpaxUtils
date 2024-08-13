using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for 3-part sequenced ranged combat moves (charge, perform, release).
	/// </summary>
	public interface IRangedCombatMove : ICombatMove
	{
		/// <summary>
		/// The prefab of the ranged projectile to instantiate.
		/// </summary>
		GameObject ProjectilePrefab { get; }

		/// <summary>
		/// The location from which the projectile must be instantiated.
		/// </summary>
		string InstanceLocation { get; }

		/// <summary>
		/// How many seconds into performing should the projectile be instantiated.
		/// </summary>
		float InstanceDelay { get; }
	}
}
