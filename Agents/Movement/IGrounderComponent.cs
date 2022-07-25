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
		/// The minimum between <see cref="SurfaceTraction"/> and <see cref="TerrainTraction"/>.
		/// </summary>
		float Traction { get; }

		/// <summary>
		/// The ability to move on the current surface.
		/// </summary>
		float SurfaceTraction { get; }

		/// <summary>
		/// The ability to move on the current terrain.
		/// </summary>
		float TerrainTraction { get; }
	}
}
