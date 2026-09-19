using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Receives lunge start, progress and end for appearance feedback, telegraphing that Stamina was spent.
	/// </summary>
	public interface ILungeFeedback
	{
		void BeginLunge(Vector3 direction);

		/// <summary>
		/// Every frame while lunging; <paramref name="intensity"/> is what the lunge committed, faded by its speed.
		/// </summary>
		void UpdateLunge(float intensity);

		void EndLunge();
	}
}
