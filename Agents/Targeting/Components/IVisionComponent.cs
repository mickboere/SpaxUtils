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
		/// Transform located at the viewpoint origin.
		/// </summary>
		Transform ViewPoint { get; }

		/// <summary>
		/// The vision's field of view.
		/// </summary>
		float FOV { get; }

		/// <summary>
		/// The furthest this entity can see.
		/// </summary>
		float Range { get; }

		/// <summary>
		/// Given <paramref name="targetables"/>, returns all targetables currently in view by this entity.
		/// </summary>
		/// <param name="targetables">The targetables to check the view for.</param>
		/// <returns>All targetables currently in view by this entity.</returns>
		List<ITargetable> Spot(IEnumerable<ITargetable> targetables);

		/// <summary>
		/// Returns the <see cref="ITargetable"/> from <paramref name="targetables"/> thats most likely meant to be targeted by the viewer right now.
		/// </summary>
		/// <param name="targetables">The targetables to check the view for.</param>
		/// <returns>The <see cref="ITargetable"/> from <paramref name="targetables"/> thats most likely meant to be targeted by the viewer right now.</returns>
		ITargetable GetMostLikelyTarget(IEnumerable<ITargetable> targetables);
	}
}
