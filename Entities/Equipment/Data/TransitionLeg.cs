namespace SpaxUtils
{
	/// <summary>
	/// Which stage of a swap an arm is in: put away what it holds, cross to the next, then come home.
	/// </summary>
	public enum TransitionLeg
	{
		None,
		ToStow,
		ToDraw,
		Recover,
		Done
	}
}
