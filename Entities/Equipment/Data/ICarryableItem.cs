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
		/// Direction this slides into its sheathe, in root-local space: out of the grip, along its length.
		/// </summary>
		Vector3 InsertAxis { get; }

		/// <summary>
		/// How far this slides in along <see cref="InsertAxis"/>. Zero for anything not drawn from a sheathe.
		/// </summary>
		float SheathedLength { get; }

		/// <summary>
		/// Where the wielding hand grips. Null means the root is the grip.
		/// </summary>
		Transform MainHand { get; }

		/// <summary>
		/// Where a second hand grips, for two-handed use. Optional.
		/// </summary>
		Transform OffHand { get; }

		/// <summary>
		/// The wielding grip itself, falling back to the root for items whose root is the grip.
		/// </summary>
		Transform Grip { get; }

		/// <summary>
		/// Where the wielding hand meets this item, in world space.
		/// </summary>
		Vector3 GripPosition { get; }

		/// <summary>
		/// Whether both ends are marked, so this item's extent is authored rather than measured.
		/// </summary>
		bool HasExtent { get; }

		/// <summary>
		/// The butt end — pommel, haft end, the bottom rim of a shield. The far side from <see cref="Tip"/>.
		/// </summary>
		Transform Butt { get; }

		/// <summary>
		/// The far end: a blade's point, a hammer's head, the top rim of a shield.
		/// </summary>
		Transform Tip { get; }

		/// <summary>
		/// Where this item's weight sits, in world space. Behind the guard for a pommel-heavy sword.
		/// </summary>
		Vector3 CenterOfMass { get; }

		/// <summary>
		/// Effective lever for rotation about the grip. Resistance to being turned goes as its square, which
		/// is why a greatsword is ponderous while a heavier, shorter hammer is not.
		/// </summary>
		float GyrationRadius { get; }

		/// <summary>
		/// Which way this points when held, in world space — the item's own line, not the hand's.
		/// </summary>
		Vector3 AimDirection { get; }

		/// <summary>
		/// Whether <see cref="AimDirection"/> means anything — whether this is pointed at things. False for
		/// a shield or a torch, which have a line but nothing to aim along it.
		/// </summary>
		bool HasAim { get; }
	}
}
