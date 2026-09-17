using UnityEngine;
using SpaxUtils;

namespace SpiritAxis
{
	/// <summary>
	/// Appearance effect requests keyed by an id object; <see cref="Clear"/> drops every request of that id.
	/// </summary>
	public interface IAppearanceEffects
	{
		void RequestFlash(object id, int prio, float weight, Color color, float amount);

		void RequestFade(object id, int prio, float weight, float fade01);

		/// <summary>
		/// One ripple shows at a time: the most recently added id wins. Pinned from floor to floor + height.
		/// </summary>
		void RequestRipple(object id, DeformRipple ripple, float floor, float height);

		/// <summary>
		/// Smear vectors sum; the strongest sets the rest. Pinned like <see cref="RequestRipple"/>.
		/// </summary>
		void RequestSmear(object id, DeformSmear smear, float floor, float height);

		void Clear(object id);
	}
}
