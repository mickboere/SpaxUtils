using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	public interface IGrounderComponent : IEntityComponent
	{
		/// <summary>
		/// Returns true when the entity is touching the ground.
		/// </summary>
		bool Grounded { get; }

		/// <summary>
		/// Normalized slope of the current surface.
		/// </summary>
		float SurfaceSlope { get; }

		/// <summary>
		/// The ability to move on the current surface.
		/// </summary>
		float SurfaceTraction { get; }

		/// <summary>
		/// Normalized slope of the current terrain.
		/// </summary>
		float TerrainSlope { get; }

		/// <summary>
		/// The ability to move on the current terrain.
		/// </summary>
		float TerrainTraction { get; }
	}
}
