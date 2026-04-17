using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Interface for shake data used in calculating screen shake.
	/// </summary>
	public interface IShakeSource : IDisposable
	{
		public const float DEFAULT_FREQUENCY = 20f;

		/// <summary>
		/// The seed to initialize the random noise with.
		/// </summary>
		int Seed { get; }

		/// <summary>
		/// X = horizontal bias for directional impact.
		/// Y = vertical bias for directional impact.
		/// Z = shake magnitude.
		/// </summary>
		Vector3 Magnitude { get; }

		/// <summary>
		/// Impact direction in world space, defines horizontal and vertical shake bias relative to the Shaker.
		/// </summary>
		Vector3 Direction { get; }

		/// <summary>
		/// The elapsed time of the shake source (defines frequency).
		/// </summary>
		float Time { get; }

		/// <summary>
		/// Whether the shake source has completed shaking.
		/// </summary>
		bool Completed { get; }

		/// <summary>
		/// Evaluates the shake intensity for the current frame.
		/// </summary>
		float EvaluateIntensity();
	}
}
