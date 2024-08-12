using System;

namespace SpaxUtils
{
	public interface IStunHandler
	{
		event Action EnteredStunEvent;
		event Action ExitedStunEvent;

		bool Stunned { get; }

		void EnterStun(HitData hitData);
	}
}
