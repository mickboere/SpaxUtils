using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for shake data used in calculating screen shake.
	/// </summary>
	public interface IShakeSource : IDisposable
	{
		/// <summary>
		/// X = left/right bias.
		/// Y = down/up bias.
		/// Z = general shake intensity.
		/// </summary>
		Vector3 Magnitude { get; }

		/// <summary>
		/// The frequency of the shaking.
		/// </summary>
		float Frequency { get; }

		/// <summary>
		/// The diminishing progress of the shake source (0 = full intensity, 1 = depleted)
		/// </summary>
		float Progress { get; }

		/// <summary>
		/// Whether the shake source has completed.
		/// </summary>
		bool Completed => Progress >= 1f;

		/// <summary>
		/// Evaluates the shake intensity for the current frame.
		/// </summary>
		float Evaluate();
	}
}
