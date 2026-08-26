using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Routes stowed equipment to the <see cref="SheathePoint"/> that should hold it.
	/// Owns no layout of its own — points do their own arranging.
	/// </summary>
	public class AgentSheatheComponent : EntityComponentMono
	{
		/// <summary>Fallback girths for equipment that declares no <see cref="ICarryableItem"/>.</summary>
		public const float DEFAULT_CARRY_RADIUS = 0.05f;
		public const float DEFAULT_WIELD_RADIUS = 0.02f;

		// Every point registered per ID, highest priority first. Keeping the losers means a point that
		// gets superseded comes back when its usurper leaves, instead of the ID going dark.
		private readonly Dictionary<string, List<SheathePoint>> points = new Dictionary<string, List<SheathePoint>>();
		private readonly Dictionary<RuntimeEquipedData, SheathePoint> assignments = new Dictionary<RuntimeEquipedData, SheathePoint>();
		private readonly List<(RuntimeEquipedData data, ArmSide owner, int order)> evacuated =
			new List<(RuntimeEquipedData, ArmSide, int)>();

		/// <summary>
		/// Registers <paramref name="point"/> under its ID. Highest <see cref="SheathePoint.Priority"/> serves,
		/// which is how an override point supersedes a body point.
		/// </summary>
		public void Register(SheathePoint point)
		{
			if (point == null || string.IsNullOrEmpty(point.ID))
			{
				return;
			}

			if (!points.TryGetValue(point.ID, out List<SheathePoint> registered))
			{
				registered = new List<SheathePoint>();
				points[point.ID] = registered;
			}
			if (registered.Contains(point))
			{
				return;
			}

			SheathePoint previous = Get(point.ID);
			registered.Add(point);
			registered.Sort((a, b) => b.Priority.CompareTo(a.Priority));
			Rehome(previous, Get(point.ID));
		}

		public void Unregister(SheathePoint point)
		{
			if (point == null || string.IsNullOrEmpty(point.ID) ||
				!points.TryGetValue(point.ID, out List<SheathePoint> registered))
			{
				return;
			}

			SheathePoint previous = Get(point.ID);
			if (!registered.Remove(point))
			{
				return;
			}
			if (registered.Count == 0)
			{
				points.Remove(point.ID);
			}

			Rehome(previous, Get(point.ID));
		}

		/// <summary>The point currently serving an ID: the highest-priority one registered.</summary>
		private SheathePoint Get(string id)
		{
			return !string.IsNullOrEmpty(id) && points.TryGetValue(id, out List<SheathePoint> registered) &&
				registered.Count > 0 ? registered[0] : null;
		}

		/// <summary>
		/// Moves everything resting on a point that just lost its ID over to the one that took over.
		/// Items already sitting on the old point are re-placed; ones still in hand keep their reservation.
		/// </summary>
		private void Rehome(SheathePoint from, SheathePoint to)
		{
			if (from == null || from == to)
			{
				return;
			}

			evacuated.Clear();
			from.Evacuate(evacuated);

			foreach ((RuntimeEquipedData data, ArmSide owner, int order) in evacuated)
			{
				if (to == null)
				{
					// Nothing serves this ID any more — drop the reservation so the next stow re-resolves.
					assignments.Remove(data);
					continue;
				}

				bool arrived = data.EquipedInstance != null &&
					data.EquipedInstance.transform.parent == from.transform;

				to.Assign(data, owner, order);
				assignments[data] = to;

				if (arrived)
				{
					to.Place(data);
				}
			}
		}

		/// <summary>
		/// Reserves a resting place for <paramref name="data"/>. The item is not moved yet —
		/// call <see cref="Place"/> once it has actually travelled there.
		/// </summary>
		/// <param name="order">Position in the stack, low first. Lets the wielded armament sit nearest the hand.</param>
		public bool TryAssign(RuntimeEquipedData data, ArmSide owner, int order = 0)
		{
			if (data == null)
			{
				return false;
			}

			if (assignments.TryGetValue(data, out SheathePoint current) && current != null)
			{
				// Already here — the order may still have changed.
				current.Assign(data, owner, order);
				return true;
			}

			SheathePoint point = Get(ResolvePointID(data, owner));
			if (point == null)
			{
				return false;
			}

			point.Assign(data, owner, order);
			assignments[data] = point;
			return true;
		}

		/// <summary>
		/// Parents <paramref name="data"/> to its reserved point and snaps it into place.
		/// </summary>
		public bool Place(RuntimeEquipedData data)
		{
			if (data == null || !assignments.TryGetValue(data, out SheathePoint point) || point == null)
			{
				return false;
			}

			point.Place(data);
			return true;
		}

		/// <summary>
		/// Gives up <paramref name="data"/>'s resting place — it is being taken in hand or removed.
		/// </summary>
		public void Release(RuntimeEquipedData data)
		{
			if (data == null)
			{
				return;
			}

			if (assignments.TryGetValue(data, out SheathePoint point) && point != null)
			{
				point.Release(data);
			}
			assignments.Remove(data);
		}

		/// <summary>
		/// Where <paramref name="data"/>'s root rests. False when it has no reserved place.
		/// </summary>
		public bool TryGetSlotOrientation(RuntimeEquipedData data, out Vector3 position, out Quaternion rotation)
		{
			if (data != null && assignments.TryGetValue(data, out SheathePoint point) && point != null)
			{
				return point.TryGetSlotOrientation(data, out position, out rotation);
			}

			position = Vector3.zero;
			rotation = Quaternion.identity;
			return false;
		}

		/// <summary>
		/// The point ID that should hold <paramref name="data"/>, given the arm it belongs to.
		/// </summary>
		private string ResolvePointID(RuntimeEquipedData data, ArmSide owner)
		{
			ICarryableItem carryable = data.Carryable;
			switch (carryable == null ? null : carryable.SheatheCategory)
			{
				case SheatheCategories.SMALL_ARMS:
					return owner == ArmSide.Left
						? SheathePointIdentifiers.ARM_SMALL_LEFT
						: SheathePointIdentifiers.ARM_SMALL_RIGHT;
				case SheatheCategories.LARGE_ARMS:
					return SheathePointIdentifiers.ARM_LARGE;
				case SheatheCategories.LARGE_ITEM:
					return SheathePointIdentifiers.ITEM_LARGE;
				default:
					// Small items live in indexed belt slots; quick-slots aren't wired yet.
					return null;
			}
		}

		/// <summary>
		/// The carry girth of a piece of equipment, from its <see cref="ICarryableItem"/>.
		/// </summary>
		public static float CarryRadiusOf(RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			return carryable != null && carryable.CarryRadius > 0f ? carryable.CarryRadius : DEFAULT_CARRY_RADIUS;
		}

		/// <summary>
		/// The grip girth of a piece of equipment, from its <see cref="ICarryableItem"/>.
		/// </summary>
		public static float WieldRadiusOf(RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			return carryable != null && carryable.WieldRadius > 0f ? carryable.WieldRadius : DEFAULT_WIELD_RADIUS;
		}

		/// <summary>
		/// This equipment's override on sheathe stacking order, from its <see cref="ICarryableItem"/>.
		/// </summary>
		public static int StackPriorityOf(RuntimeEquipedData data)
		{
			ICarryableItem carryable = data == null ? null : data.Carryable;
			return carryable == null ? 0 : carryable.StackPriority;
		}
	}
}
