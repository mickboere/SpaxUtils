namespace SpaxUtils
{
	/// <summary>
	/// Implemented by behaviours that close distance before swinging. The lunge runs its OWN clock, separate
	/// from charge and run time, so a timeline can play a Lunging region across it however far it travels.
	/// </summary>
	public interface ILungeProvider
	{
		/// <summary>Whether a lunge is currently closing the gap.</summary>
		bool Lunging { get; }

		/// <summary>
		/// Normalized estimate of progress toward impact, reaching 1 as the swing is about to be released.
		/// Estimated rather than measured so it leads into the swing regardless of distance or speed.
		/// </summary>
		float LungeProgress { get; }
	}
}
