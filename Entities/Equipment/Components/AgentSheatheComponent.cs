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

		private readonly Dictionary<string, SheathePoint> points = new Dictionary<string, SheathePoint>();
		private readonly Dictionary<RuntimeEquipedData, SheathePoint> assignments = new Dictionary<RuntimeEquipedData, SheathePoint>();

		/// <summary>
		/// Registers <paramref name="point"/> under its ID. Highest <see cref="SheathePoint.Priority"/> wins,
		/// which is how an override point supersedes a body point.
		/// </summary>
		public void Register(SheathePoint point)
		{
			if (point == null || string.IsNullOrEmpty(point.ID))
			{
				return;
			}

			if (points.TryGetValue(point.ID, out SheathePoint existing) &&
				existing != null && existing.Priority >= point.Priority)
			{
				return;
			}

			points[point.ID] = point;
		}

		public void Unregister(SheathePoint point)
		{
			if (point == null || string.IsNullOrEmpty(point.ID))
			{
				return;
			}

			if (points.TryGetValue(point.ID, out SheathePoint existing) && existing == point)
			{
				points.Remove(point.ID);
			}
		}

		private SheathePoint Get(string id)
		{
			return string.IsNullOrEmpty(id) || !points.TryGetValue(id, out SheathePoint point) ? null : point;
		}

		/// <summary>
		/// Reserves a resting place for <paramref name="data"/>. The item is not moved yet —
		/// call <see cref="Place"/> once it has actually travelled there.
		/// </summary>
		public bool TryAssign(RuntimeEquipedData data, ArmSide owner)
		{
			if (data == null)
			{
				return false;
			}

			if (assignments.TryGetValue(data, out SheathePoint current) && current != null)
			{
				return true;
			}

			SheathePoint point = Get(ResolvePointID(data, owner));
			if (point == null)
			{
				return false;
			}

			point.Assign(data, owner);
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
			switch (data.EquipmentData.SheatheCategory)
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
	}
}
