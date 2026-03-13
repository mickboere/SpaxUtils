using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that is able to spot <see cref="ITargetable"/>s.
	/// </summary>
	public interface IVisionComponent : IEntityComponent
	{
		/// <summary>
		/// The furthest this entity can see.
		/// </summary>
		float Range { get; }

		/// <summary>
		/// Returns the viewpoint transform used for vision checks.
		/// Uses the agent's eye transform by default.
		/// </summary>
		/// <param name="useCameraIfAvailable">If true and a camera is present, returns the camera transform instead.</param>
		Transform GetViewPoint(bool useCameraIfAvailable = false);

		/// <summary>
		/// Returns the field of view used for vision checks.
		/// Uses the agent's configured FOV by default.
		/// </summary>
		/// <param name="useCameraIfAvailable">If true and a camera is present, returns the camera's field of view instead.</param>
		float GetFOV(bool useCameraIfAvailable = false);

		/// <summary>
		/// Given <paramref name="targetables"/>, returns all targetables currently in view by this entity.
		/// </summary>
		/// <param name="targetables">The targetables to check the view for.</param>
		/// <param name="useCameraIfAvailable">If true and a camera is present, uses the camera as the viewpoint instead of the eye transform.</param>
		List<ITargetable> Spot(IEnumerable<ITargetable> targetables, bool useCameraIfAvailable = false);

		/// <summary>
		/// Returns the <see cref="ITargetable"/> from <paramref name="targetables"/> thats most likely meant to be targeted by the viewer right now.
		/// </summary>
		/// <param name="targetables">The targetables to check the view for.</param>
		/// <param name="useCameraIfAvailable">If true and a camera is present, uses the camera as the viewpoint instead of the eye transform.</param>
		ITargetable GetMostLikelyTarget(IEnumerable<ITargetable> targetables, bool useCameraIfAvailable = false);
	}
}
