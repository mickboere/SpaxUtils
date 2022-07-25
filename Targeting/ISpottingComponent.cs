using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="IEntityComponent"/> that is able to spot <see cref="ITargetable"/>s.
	/// </summary>
	public interface ISpottingComponent : IEntityComponent
	{
		List<ITargetable> Spot(List<ITargetable> targetables);
	}
}
