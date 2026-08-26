using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A place on the body where equipment rests when it isn't in hand. Authored flush with the skin —
	/// each item is pushed out by its own carry radius, and siblings stack sideways from there.
	/// </summary>
	public class SheathePoint : MonoBehaviour
	{
		/// <summary>
		/// Which point this is. Fixed identifiers so a point can be addressed and overridden directly.
		/// </summary>
		public string ID => id;

		/// <summary>
		/// Highest priority wins when several points claim the same <see cref="ID"/>.
		/// </summary>
		public int Priority => priority;

		[SerializeField, ConstDropdown(typeof(ISheathePointIdentifiers))]
		private string id;
		[SerializeField, Tooltip("Highest wins when several points share an ID. Lets armour override a body point.")]
		private int priority;

		[Header("Layout")]
		[SerializeField, Tooltip("Local direction away from the body. Each item is pushed out along it by its carry radius.")]
		private Vector3 outwardAxis = Vector3.up;
		[SerializeField, Tooltip("Local direction siblings stack along.")]
		private Vector3 stackAxis = Vector3.forward;
		[SerializeField, Tooltip("Extra gap between stacked items, in metres.")]
		private float spacing;
		[SerializeField, Tooltip("Local axis each stacked item is progressively rotated around, so they fan instead of overlapping.")]
		private Vector3 rotationAxis = Vector3.right;
		[SerializeField, Tooltip("Degrees of fan per stack index.")]
		private float rotationStep;

		[Header("Owner")]
		[SerializeField, Tooltip("Angle each item toward the arm that draws it. For the shared back point.")]
		private bool rotateTowardOwner;
		[SerializeField, Conditional(nameof(rotateTowardOwner)), Tooltip("Degrees to angle toward the owning arm.")]
		private float ownerAngle = 20f;

		[SerializeField] private bool drawGizmos;

		private readonly List<RuntimeEquipedData> entries = new List<RuntimeEquipedData>();
		private readonly Dictionary<RuntimeEquipedData, ArmSide> owners = new Dictionary<RuntimeEquipedData, ArmSide>();
		private readonly Dictionary<RuntimeEquipedData, int> orders = new Dictionary<RuntimeEquipedData, int>();
		private readonly Dictionary<RuntimeEquipedData, int> sequence = new Dictionary<RuntimeEquipedData, int>();
		private readonly List<RuntimeEquipedData> present = new List<RuntimeEquipedData>();
		private int nextSequence;

		private AgentSheatheComponent sheathe;

		public void InjectDependencies([Optional] AgentSheatheComponent sheathe)
		{
			this.sheathe = sheathe;
		}

		protected void OnEnable()
		{
			if (sheathe != null)
			{
				sheathe.Register(this);
			}
		}

		protected void OnDisable()
		{
			if (sheathe != null)
			{
				sheathe.Unregister(this);
			}
		}

		/// <summary>
		/// Reserves a place for <paramref name="data"/> and re-lays out the stack. Re-assigning an item
		/// already here just updates its <paramref name="order"/>. Does not reparent — the caller decides
		/// when the item actually arrives.
		/// </summary>
		/// <param name="order">Position in the stack, low first. Lets the wielded armament sit nearest the hand.</param>
		public void Assign(RuntimeEquipedData data, ArmSide owner, int order = 0)
		{
			if (data == null)
			{
				return;
			}

			if (!entries.Contains(data))
			{
				entries.Add(data);
				sequence[data] = nextSequence++;
			}
			else if (owners.TryGetValue(data, out ArmSide knownOwner) &&
				knownOwner == owner && orders.TryGetValue(data, out int knownOrder) && knownOrder == order)
			{
				// Nothing moved.
				return;
			}

			owners[data] = owner;
			orders[data] = order;
			Sort();
			Layout();
		}

		/// <summary>
		/// Frees <paramref name="data"/>'s place and closes the gap.
		/// </summary>
		public void Release(RuntimeEquipedData data)
		{
			if (data == null || !entries.Remove(data))
			{
				return;
			}

			owners.Remove(data);
			orders.Remove(data);
			sequence.Remove(data);
			Layout();
		}

		/// <summary>
		/// Hands this point's whole contents over and empties it, for when another point supersedes it.
		/// The items are not unparented — the receiver decides where each one goes.
		/// </summary>
		public void Evacuate(List<(RuntimeEquipedData data, ArmSide owner, int order)> into)
		{
			foreach (RuntimeEquipedData data in entries)
			{
				owners.TryGetValue(data, out ArmSide owner);
				orders.TryGetValue(data, out int order);
				into.Add((data, owner, order));
			}

			entries.Clear();
			owners.Clear();
			orders.Clear();
			sequence.Clear();
		}

		/// <summary>
		/// An item's own stack priority wins outright — a shield rides above the weapons whichever one is
		/// next out. Within a priority the next-out armament leads, and assignment sequence breaks ties.
		/// Priority sorts DESCENDING: the stack builds away from the point, so the lowest ends up on top.
		/// </summary>
		private void Sort()
		{
			entries.Sort((a, b) =>
			{
				int compare = AgentSheatheComponent.StackPriorityOf(b).CompareTo(AgentSheatheComponent.StackPriorityOf(a));
				if (compare != 0)
				{
					return compare;
				}

				compare = OrderOf(a).CompareTo(OrderOf(b));
				return compare != 0 ? compare : sequence[a].CompareTo(sequence[b]);
			});
		}

		private int OrderOf(RuntimeEquipedData data)
		{
			return orders.TryGetValue(data, out int order) ? order : int.MaxValue;
		}

		/// <summary>
		/// Where <paramref name="data"/>'s root sits when resting at this point. For an item still on its
		/// way over, this is the place being held for it — the others only shuffle aside once it lands.
		/// </summary>
		public bool TryGetSlotOrientation(RuntimeEquipedData data, out Vector3 position, out Quaternion rotation)
		{
			List<RuntimeEquipedData> slots = Present(data);
			int index = slots.IndexOf(data);
			if (index < 0)
			{
				position = transform.position;
				rotation = transform.rotation;
				return false;
			}

			(position, rotation) = SlotAt(slots, index);
			return true;
		}

		/// <summary>
		/// Moves every item that has actually arrived to its slot. Items still travelling are not counted,
		/// so the stack stays put until the hand delivers.
		/// </summary>
		public void Layout()
		{
			List<RuntimeEquipedData> slots = Present(null);
			for (int i = 0; i < slots.Count; i++)
			{
				(Vector3 pos, Quaternion rot) = SlotAt(slots, i);
				slots[i].EquipedInstance.transform.SetPositionAndRotation(pos, rot);
			}
		}

		/// <summary>
		/// The items occupying this point: everything that has arrived, plus <paramref name="incoming"/>
		/// so a travelling item can be told where it will end up.
		/// </summary>
		private List<RuntimeEquipedData> Present(RuntimeEquipedData incoming)
		{
			present.Clear();
			for (int i = 0; i < entries.Count; i++)
			{
				if (HasArrived(entries[i]) || entries[i] == incoming)
				{
					present.Add(entries[i]);
				}
			}
			return present;
		}

		private bool HasArrived(RuntimeEquipedData data)
		{
			return data.EquipedInstance != null && data.EquipedInstance.transform.parent == transform;
		}

		/// <summary>
		/// Parents <paramref name="data"/>'s visual to this point and snaps it to its slot.
		/// </summary>
		public void Place(RuntimeEquipedData data)
		{
			if (data == null || data.EquipedInstance == null)
			{
				return;
			}

			// Parent first: that is what makes it count as arrived, so the re-layout seats the whole
			// stack around it — the others shove over exactly now, and not a moment earlier.
			data.EquipedInstance.transform.SetParent(transform);
			Layout();
		}

		/// <summary>
		/// The world orientation of stack index <paramref name="index"/>.
		/// Worked out in world space so odd bone scales can't distort the spacing.
		/// </summary>
		private (Vector3 pos, Quaternion rot) SlotAt(List<RuntimeEquipedData> list, int index)
		{
			Vector3 outward = transform.TransformDirection(outwardAxis).normalized;
			Vector3 stack = transform.TransformDirection(stackAxis).normalized;

			// The first item sits on the point itself; each next one clears its neighbour by both radii.
			float along = 0f;
			for (int i = 1; i <= index; i++)
			{
				along += AgentSheatheComponent.CarryRadiusOf(list[i - 1])
					+ AgentSheatheComponent.CarryRadiusOf(list[i])
					+ spacing;
			}

			float radius = AgentSheatheComponent.CarryRadiusOf(list[index]);
			Vector3 position = transform.position + stack * along + outward * radius;

			Quaternion rotation = transform.rotation;
			if (rotationStep != 0f)
			{
				rotation = Quaternion.AngleAxis(rotationStep * index, transform.TransformDirection(rotationAxis)) * rotation;
			}
			if (rotateTowardOwner && owners.TryGetValue(list[index], out ArmSide owner) && owner != ArmSide.None)
			{
				rotation = Quaternion.AngleAxis(owner == ArmSide.Left ? -ownerAngle : ownerAngle, outward) * rotation;
			}

			return (position, rotation);
		}

		// Cyan = outward (off the body), magenta = stack (sibling spacing), yellow = rotation axis + fan.
		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
			{
				return;
			}

			const float length = 0.1f;
			Vector3 origin = transform.position;
			Vector3 outward = transform.TransformDirection(outwardAxis).normalized;
			Vector3 stack = transform.TransformDirection(stackAxis).normalized;
			Vector3 rotation = transform.TransformDirection(rotationAxis).normalized;

			Gizmos.color = Color.cyan;
			Gizmos.DrawSphere(origin, 0.01f);
			Gizmos.DrawRay(origin, outward * length);
			Gizmos.color = Color.magenta;
			Gizmos.DrawRay(origin, stack * length);

			// Where a default-sized item rests: tangent to the point, pushed out by its own radius.
			Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
			Gizmos.DrawWireSphere(origin + outward * AgentSheatheComponent.DEFAULT_CARRY_RADIUS,
				AgentSheatheComponent.DEFAULT_CARRY_RADIUS);

			// The axis items fan around, drawn through the point since they turn both ways.
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(origin - rotation * length * 0.5f, origin + rotation * length * 0.5f);

			if (rotationStep == 0f)
			{
				return;
			}

			// Preview the fan: where the first few stacked items end up pointing.
			for (int i = 1; i <= 3; i++)
			{
				Gizmos.color = new Color(1f, 1f, 0f, 1f - i * 0.25f);
				Gizmos.DrawRay(origin + stack * (length * 0.3f * i),
					Quaternion.AngleAxis(rotationStep * i, rotation) * outward * (length * 0.6f));
			}
		}
	}
}
