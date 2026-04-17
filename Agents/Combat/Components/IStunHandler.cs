using System;

namespace SpaxUtils
{
	public interface IStunHandler
	{
		event Action EnteredStunEvent;
		event Action ExitedStunEvent;

		bool Stunned { get; }

		/// <summary>
		/// Have the entity enter its stunned state.
		/// Make sure necessary forces are applied BEFORE entering stun.
		/// </summary>
		void EnterStun(HitData hitData, float duration = -1f);
	}
}
