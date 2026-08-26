using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Something carried on the body: how wide it is at rest, how thick its grip is, and where that grip is.
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

		/// <summary>
		/// How this is carried when it is not in hand. The body plan decides which point that maps to.
		/// </summary>
		string SheatheCategory { get; }

		/// <summary>
		/// Overrides where this sits in a sheathe stack. Lower stays nearer the top, ahead of even the
		/// armament that is next out.
		/// </summary>
		int StackPriority { get; }

		/// <summary>
		/// Where the wielding hand grips. Null means the root is the grip.
		/// </summary>
		Transform MainHand { get; }

		/// <summary>
		/// Where a second hand grips, for two-handed use. Optional.
		/// </summary>
		Transform OffHand { get; }
	}
}
