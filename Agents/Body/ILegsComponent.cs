using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	public interface ILegsComponent : IEntityComponent
	{
		event Action<ILeg, bool> FootstepEvent;

		IReadOnlyList<ILeg> Legs { get; }
	}
}
