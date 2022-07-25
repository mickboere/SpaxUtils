using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for classes containing data of a single pose.
	/// </summary>
	public interface IPose : IWeightedElement
	{
		/// <summary>
		/// The animation clip of this pose.
		/// </summary>
		AnimationClip Clip { get; }

		/// <summary>
		/// Should the <see cref="Clip"/> be mirrored.
		/// </summary>
		bool Mirror { get; }

		/// <summary>
		/// The duration of this pose in seconds.
		/// </summary>
		float Duration { get; }

		/// <summary>
		/// The optional data of this pose.
		/// </summary>
		ILabeledDataProvider Data { get; }

		/// <summary>
		/// Evaluate the transition amount to this pose where <paramref name="x"/> is progress from another (0) to this pose (1).
		/// </summary>
		/// <param name="x">The progress from another (0) to this pose (1).</param>
		/// <returns>The transition amount corresponding to <paramref name="x"/> amount of progress.</returns>
		float EvaluateTransition(float x);
	}
}
