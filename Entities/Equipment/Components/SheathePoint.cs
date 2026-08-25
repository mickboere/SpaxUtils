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
		/// Reserves a slot for <paramref name="data"/> and re-lays out the stack.
		/// Does not reparent — the caller decides when the item actually arrives.
		/// </summary>
		public void Assign(RuntimeEquipedData data, ArmSide owner)
		{
			if (data == null || entries.Contains(data))
			{
				return;
			}

			entries.Add(data);
			owners[data] = owner;
			Layout();
		}

		/// <summary>
		/// Frees <paramref name="data"/>'s slot and closes the gap.
		/// </summary>
		public void Release(RuntimeEquipedData data)
		{
			if (data == null || !entries.Remove(data))
			{
				return;
			}

			owners.Remove(data);
			Layout();
		}

		/// <summary>
		/// Where <paramref name="data"/>'s root sits when resting at this point.
		/// </summary>
		public bool TryGetSlotOrientation(RuntimeEquipedData data, out Vector3 position, out Quaternion rotation)
		{
			int index = entries.IndexOf(data);
			if (index < 0)
			{
				position = transform.position;
				rotation = transform.rotation;
				return false;
			}

			(position, rotation) = SlotAt(index);
			return true;
		}

		/// <summary>
		/// Moves every assigned item that has already arrived to its current slot.
		/// </summary>
		public void Layout()
		{
			for (int i = 0; i < entries.Count; i++)
			{
				GameObject visual = entries[i].EquipedInstance;
				if (visual == null || visual.transform.parent != transform)
				{
					// Not here yet — it is still in hand, on its way over.
					continue;
				}

				(Vector3 pos, Quaternion rot) = SlotAt(i);
				visual.transform.SetPositionAndRotation(pos, rot);
			}
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

			data.EquipedInstance.transform.SetParent(transform);
			if (TryGetSlotOrientation(data, out Vector3 pos, out Quaternion rot))
			{
				data.EquipedInstance.transform.SetPositionAndRotation(pos, rot);
			}
		}

		/// <summary>
		/// The world orientation of stack index <paramref name="index"/>.
		/// Worked out in world space so odd bone scales can't distort the spacing.
		/// </summary>
		private (Vector3 pos, Quaternion rot) SlotAt(int index)
		{
			Vector3 outward = transform.TransformDirection(outwardAxis).normalized;
			Vector3 stack = transform.TransformDirection(stackAxis).normalized;

			float along = 0f;
			for (int i = 0; i < index; i++)
			{
				along += AgentSheatheComponent.CarryRadiusOf(entries[i]) * 2f + spacing;
			}
			float radius = AgentSheatheComponent.CarryRadiusOf(entries[index]);
			along += radius;

			Vector3 position = transform.position + stack * along + outward * radius;

			Quaternion rotation = transform.rotation;
			if (rotationStep != 0f)
			{
				rotation = Quaternion.AngleAxis(rotationStep * index, transform.TransformDirection(rotationAxis)) * rotation;
			}
			if (rotateTowardOwner && owners.TryGetValue(entries[index], out ArmSide owner) && owner != ArmSide.None)
			{
				rotation = Quaternion.AngleAxis(owner == ArmSide.Left ? -ownerAngle : ownerAngle, outward) * rotation;
			}

			return (position, rotation);
		}

		protected void OnDrawGizmos()
		{
			if (!drawGizmos)
			{
				return;
			}

			Gizmos.color = Color.cyan;
			Gizmos.DrawSphere(transform.position, 0.01f);
			Gizmos.DrawRay(transform.position, transform.TransformDirection(outwardAxis).normalized * 0.1f);
			Gizmos.color = Color.magenta;
			Gizmos.DrawRay(transform.position, transform.TransformDirection(stackAxis).normalized * 0.1f);
		}
	}
}
