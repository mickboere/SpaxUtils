﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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
		[SerializeField] private ArmedSettings defaultSettings;
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

		protected void Awake()
		{
			if (left)
			{
				equipment.AddSlot(new EquipmentSlot(HumanBoneIdentifiers.LEFT_HAND, leftType, LeftHand, () => GetHandSlotOrientation(true, true)));
			}

			if (right)
			{
				equipment.AddSlot(new EquipmentSlot(HumanBoneIdentifiers.RIGHT_HAND, rightType, RightHand, () => GetHandSlotOrientation(false, true)));
			}
		}

		protected void OnEnable()
		{
			foreach (RuntimeEquipedData equipedData in equipment.EquipedItems)
			{
				OnEquipedEvent(equipedData);
			}

			equipment.EquipedEvent += OnEquipedEvent;
			equipment.UnequipingEvent += OnUnequipingEvent;
		}

		protected void OnDisable()
		{
			foreach (RuntimeEquipedData equipedData in equipment.EquipedItems)
			{
				OnUnequipingEvent(equipedData);
			}

			equipment.EquipedEvent -= OnEquipedEvent;
			equipment.UnequipingEvent -= OnUnequipingEvent;
		}

		protected void OnDestroy()
		{
			equipment.RemoveSlot(HumanBoneIdentifiers.LEFT_HAND);
			equipment.RemoveSlot(HumanBoneIdentifiers.RIGHT_HAND);
		}

		public void UpdateArm(bool isLeft, float weight, ArmedSettings settings, float delta)
		{
			if (isLeft)
			{
				leftHelper?.Update(weight, settings, delta);
			}
			else
			{
				rightHelper?.Update(weight, settings, delta);
			}
		}

		public void UpdateArms(float weight, ArmedSettings settings, float delta)
		{
			leftHelper?.Update(weight, settings, delta);
			rightHelper?.Update(weight, settings, delta);
		}

		public (Vector3 pos, Quaternion rot) GetHandSlotOrientation(bool isLeft, bool local)
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

		private void OnEquipedEvent(RuntimeEquipedData data)
		{
			OnUnequipingEvent(data);

			if (left && data.Slot.UID == HumanBoneIdentifiers.LEFT_HAND)
			{
				leftHelper = new ArmSlotHelper(true, this, ik, lookup, rigidbodyWrapper);
			}
			else if (right && data.Slot.UID == HumanBoneIdentifiers.RIGHT_HAND)
			{
				rightHelper = new ArmSlotHelper(false, this, ik, lookup, rigidbodyWrapper);
			}
		}

		private void OnUnequipingEvent(RuntimeEquipedData data)
		{
			if (data.Slot.UID == HumanBoneIdentifiers.LEFT_HAND)
			{
				leftHelper?.Dispose();
			}
			else if (data.Slot.UID == HumanBoneIdentifiers.RIGHT_HAND)
			{
				rightHelper?.Dispose();
			}
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
