namespace SpaxUtils
{
	/// <summary>How a leg takes the hand off the animation and hands it back.</summary>
	public enum LegBlend
	{
		/// <summary>Taking over: the leg ramps in from wherever the animation has the hand.</summary>
		In,

		/// <summary>Already owned: the leg carries on from the one before it at full weight.</summary>
		Hold,

		/// <summary>Handing back: the leg follows the hand home and is spent by the time it arrives.</summary>
		Out
	}
}
