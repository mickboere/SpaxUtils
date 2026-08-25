namespace SpaxUtils
{
	/// <summary>
	/// The two girths of something carried on the body: how wide it is at rest, and how thick its grip is.
	/// A hammer clears the body by its head but is held by its handle.
	/// </summary>
	public interface ICarryableItem
	{
		/// <summary>
		/// Radius of the widest part. Sheathe points push each item out by it and stack siblings by it.
		/// </summary>
		float CarryRadius { get; }

		/// <summary>
		/// Radius of the grip — how far off the palm the held object's axis sits.
		/// </summary>
		float WieldRadius { get; }
	}
}
