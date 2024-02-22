namespace SpaxUtils
{
	public interface IGrounderComponent : IEntityComponent
	{
		/// <summary>
		/// Whether this entity should ground itself.
		/// </summary>
		bool Ground { get; set; }

		/// <summary>
		/// Returns true when the entity is touching the ground.
		/// </summary>
		bool Grounded { get; }

		/// <summary>
		/// Grounded percentage: 1 = fully grounded, 0 = ground exceeds reach.
		/// </summary>
		float GroundedAmount { get; }

		/// <summary>
		/// The amount of gravitational force applied to the agent.
		/// </summary>
		CompositeFloat Gravity { get; }

		/// <summary>
		/// Returns true when the entity is unable to move due to too low <see cref="Traction"/>.
		/// </summary>
		bool Sliding { get; }

		/// <summary>
		/// Normalized slope of the average surface normal.
		/// </summary>
		float SurfaceSlope { get; }

		/// <summary>
		/// Normalized slope of the average terrain height.
		/// </summary>
		float TerrainSlope { get; }

		/// <summary>
		/// The grip on the current surface.
		/// When too low, <see cref="Sliding"/> will be true.
		/// </summary>
		float Traction { get; }

		/// <summary>
		/// The ability to move on the current terrain.
		/// Running down-hill may increase mobility, up-hill decrease. Stuff like walking in water may also decrease it.
		/// </summary>
		float Mobility { get; }

		/// <summary>
		/// The added elevation to the grounded position.
		/// </summary>
		public float Elevation { get; set; }
	}
}
