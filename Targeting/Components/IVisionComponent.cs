using System.Collections.Generic;

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
		/// Given <paramref name="targetables"/>, returns all targetables currently in view by this entity.
		/// </summary>
		/// <param name="targetables">The targetables to check the view for.</param>
		/// <returns></returns>
		List<ITargetable> Spot(List<ITargetable> targetables);
	}
}
