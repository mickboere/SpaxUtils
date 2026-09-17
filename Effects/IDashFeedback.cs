using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Receives dash start, progress and end for appearance feedback, like a launch ripple and speed smear.
	/// </summary>
	public interface IDashFeedback
	{
		void BeginDash(Vector3 direction);

		/// <summary>
		/// Every frame while dashing; <paramref name="intensity"/> is 0 at run speed and 1 at full dash speed.
		/// </summary>
		void UpdateDash(float intensity);

		void EndDash();
	}
}
