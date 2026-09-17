using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Receives storm leap start, progress and end for appearance feedback, like a trailing smear.
	/// </summary>
	public interface IStormFeedback
	{
		void BeginStorm(Vector3 direction);

		/// <summary>
		/// Every frame while storming; <paramref name="intensity"/> is the leap's speed over its target speed (0-1).
		/// </summary>
		void UpdateStorm(float intensity);

		void EndStorm();
	}
}
