using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class ArmSlotsComponent : EntityComponentBase
	{
		protected Transform LeftHand => lookup.Lookup(HumanBoneIdentifiers.LEFT_HAND);
		protected Transform RightHand => lookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND);

		protected TransformLookup SafeLookup
		{
			get
			{
				if (lookup == null)
				{
					lookup = gameObject.GetComponentRelative<TransformLookup>();
				}
				if (lookup == null)
				{
					lookup = gameObject.AddComponent<TransformLookup>();
				}
				return lookup;
			}
		}

		[SerializeField, HideInInspector] private bool left;
		[SerializeField, Conditional(nameof(left), drawToggle: true), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string leftType;
		[SerializeField, HideInInspector] private bool right;
		[SerializeField, Conditional(nameof(right), drawToggle: true), ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string rightType;
		[SerializeField] private Vector3 handSlotPointOffset = new Vector3(-0.01f, 0f, 0f);
		[SerializeField] private bool drawGizmos;
		[Header("DEBUGGING")]
		[SerializeField] private GameObject testPrefab;
		[SerializeField] private bool testLeft;
		[SerializeField] private bool testRight;

		private TransformLookup lookup;
		private IEquipmentComponent equipment;
		private IIKComponent ik;
		private RigidbodyWrapper rigidbodyWrapper;

		private ArmSlotHelper leftHelper;
		private ArmSlotHelper rightHelper;

		public void InjectDependencies(TransformLookup lookup,
			IEquipmentComponent equipment, IIKComponent ik,
			RigidbodyWrapper rigidbodyWrapper)
		{
			this.lookup = lookup;
			this.equipment = equipment;
			this.ik = ik;
			this.rigidbodyWrapper = rigidbodyWrapper;
		}

		#region Editor
#if UNITY_EDITOR
		protected void OnValidate()
		{
			if (testLeft)
			{
				InstantiateTest(true);
				testLeft = false;
			}

			if (testRight)
			{
				InstantiateTest(false);
				testRight = false;
			}
		}

		private void InstantiateTest(bool left)
		{
			GameObject instance = Instantiate(testPrefab, left ?
				SafeLookup.Lookup(HumanBoneIdentifiers.LEFT_HAND) :
				SafeLookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND));
			instance.transform.localScale = Vector3.one.Divide(instance.transform.lossyScale);
			(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(left, false);
			instance.transform.position = orientation.pos;
			instance.transform.rotation = orientation.rot;
		}
#endif
		#endregion

		protected void Awake()
		{
			if (left) { CreateSlot(true); }
			if (right) { CreateSlot(false); }

			void CreateSlot(bool isLeft)
			{
				equipment.AddSlot(new EquipmentSlot(
					isLeft ? HumanBoneIdentifiers.LEFT_HAND : HumanBoneIdentifiers.RIGHT_HAND,
					isLeft ? leftType : rightType,
					(data) => OnEquip(isLeft, data),
					(data) => OnUnequip(isLeft, data)));
			}
		}

		protected void OnDestroy()
		{
			equipment.RemoveSlot(HumanBoneIdentifiers.LEFT_HAND);
			equipment.RemoveSlot(HumanBoneIdentifiers.RIGHT_HAND);
		}

		/// <summary>
		/// Updates one of the arms to animate with the defined parameters.
		/// </summary>
		/// <param name="isLeft">Whether to update the left arm or the right.</param>
		/// <param name="weight">Effective weight of the arm's IK.</param>
		/// <param name="settings">Armed settings defining how an arm should be carried.</param>
		/// <param name="delta">Delta time used for interpolation and smoothing.</param>
		public void UpdateArm(bool isLeft, float weight, ArmedSettings settings, float delta)
		{
			if (isLeft) { leftHelper?.Update(weight, settings, delta); }
			else { rightHelper?.Update(weight, settings, delta); }
		}

		/// <summary>
		/// Resets the arm helpers to disable IK.
		/// </summary>
		public void ResetArms()
		{
			leftHelper?.Reset();
			rightHelper?.Reset();
		}

		private void OnEquip(bool isLeft, RuntimeEquipedData data)
		{
			// Parent and position.
			if (data.EquipedVisual != null)
			{
				Transform parent = isLeft ? LeftHand : RightHand;
				(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(isLeft, true);
				data.EquipedVisual.transform.SetParent(parent);
				data.EquipedVisual.transform.localPosition = orientation.pos;
				data.EquipedVisual.transform.localRotation = orientation.rot;
			}

			// Create helper.
			if (isLeft) { leftHelper = new ArmSlotHelper(true, GetHandSlotOrientation, ik, lookup, rigidbodyWrapper); }
			else { rightHelper = new ArmSlotHelper(false, GetHandSlotOrientation, ik, lookup, rigidbodyWrapper); }
		}

		private void OnUnequip(bool isLeft, RuntimeEquipedData data)
		{
			// Dispose helper.
			if (isLeft) { leftHelper?.Dispose(); }
			else { rightHelper?.Dispose(); }
		}

		private (Vector3 pos, Quaternion rot) GetHandSlotOrientation(bool isLeft, bool local)
		{
			// Calculate position.
			Transform hand = isLeft ? LeftHand : RightHand;
			Vector3 handPos = hand.position;
			Vector3 middleFPos = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_MIDDLE_PROXIMAL : HumanBoneIdentifiers.RIGHT_MIDDLE_PROXIMAL).position;
			Vector3 position = Vector3.Lerp(handPos, middleFPos, 0.8f);

			// Calculate rotation.
			Vector3 thumb = lookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_THUMB_PROXIMAL : HumanBoneIdentifiers.RIGHT_THUMB_PROXIMAL).position;
			Vector3 handToMiddleF = Vector3.Normalize(middleFPos - handPos);
			Vector3 handToThumb = Vector3.Normalize(thumb - handPos);
			Quaternion rotation = Quaternion.LookRotation(handToThumb, -handToMiddleF);

			Vector3 offset = isLeft ? handSlotPointOffset.MirrorX() : handSlotPointOffset;
			position += rotation * offset;

			if (local)
			{
				// Convert to local.
				position = hand.InverseTransformPoint(position);
				rotation = Quaternion.Inverse(hand.rotation) * rotation;
			}

			return (position, rotation);
		}

		#region Gizmos

		protected void OnDrawGizmos()
		{
			if (drawGizmos)
			{
				DrawHandSlotGizmos();
				leftHelper?.DrawGizmos();
				rightHelper?.DrawGizmos();
			}
		}

		private void DrawHandSlotGizmos()
		{
			if (lookup == null)
			{
				lookup = gameObject.GetComponentRelative<TransformLookup>();
			}
			if (lookup != null)
			{
				Draw(true);
				Draw(false);

				void Draw(bool isLeft, float size = 0.3f)
				{
					(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(isLeft, false);
					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere(orientation.pos, 0.02f);
					Gizmos.color = Color.blue;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.forward * size);
					Gizmos.color = Color.red;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * (isLeft ? Vector3.left : Vector3.right) * size);
					Gizmos.color = Color.green;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.up * size);
				}
			}
		}

		#endregion
	}
}
