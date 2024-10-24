﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Agent component which supplies equipment slots for a left and right arm,
	/// and takes care of parenting and positioning of arms equipment.
	/// </summary>
	public class AgentArmsComponent : AgentComponentBase
	{
		/// <summary>
		/// Invoked when the sheathed state is changed.
		/// </summary>
		public event Action<bool> SheathedEvent;

		/// <summary>
		/// Composite weight float for both arms that can be influenced by external modifiers.
		/// </summary>
		public CompositeFloat Weight { get; set; } = new CompositeFloat(1f);

		public Transform LeftHand => lookup.Lookup(HumanBoneIdentifiers.LEFT_HAND);
		public Transform RightHand => lookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND);
		public Transform LeftSheathe => lookup.Lookup(TransformLookupIdentifiers.LEFT_SHEATHE);
		public Transform RightSheathe => lookup.Lookup(TransformLookupIdentifiers.RIGHT_SHEATHE);

		public bool Sheathed { get; private set; }
		public RuntimeEquipedData LeftEquip => leftEquip;
		public RuntimeEquipedData RightEquip => rightEquip;
		public GameObject LeftVisual => LeftEquip == null ? null : LeftEquip.EquipedInstance;
		public GameObject RightVisual => RightEquip == null ? null : RightEquip.EquipedInstance;

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

		private RuntimeEquipedData leftEquip;
		private RuntimeEquipedData rightEquip;

		public void InjectDependencies(IEquipmentComponent equipment, TransformLookup lookup)
		{
			this.equipment = equipment;
			this.lookup = lookup;
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
		/// Sheathes or unsheathes the equiped arms.
		/// </summary>
		/// <param name="sheathe">TRUE will sheathe the arms, FALSE will unsheathe the arms.</param>
		public void SheatheArms(bool sheathe)
		{
			if (leftEquip != null && leftEquip.EquipedInstance != null)
			{
				SetParentAndOrientation(leftEquip.EquipedInstance.transform, true, sheathe);
			}
			if (rightEquip != null && rightEquip.EquipedInstance != null)
			{
				SetParentAndOrientation(rightEquip.EquipedInstance.transform, false, sheathe);
			}

			Sheathed = sheathe;
			SheathedEvent?.Invoke(Sheathed);
		}

		/// <summary>
		/// Set the parent and orientation of <paramref name="transform"/> as if it were a piece of arms.
		/// </summary>
		/// <param name="transform">The transform to set.</param>
		/// <param name="left">Whether to treat it as left arms (true) or right arms (false).</param>
		/// <param name="sheathe">Whether to sheath the transform or unsheathe it (in hand).</param>
		public void SetParentAndOrientation(Transform transform, bool left, bool sheathe)
		{
			if (sheathe)
			{
				// Place in sheathe.
				Transform parent = left ? LeftSheathe : RightSheathe;
				transform.SetParent(parent);
				transform.localPosition = Vector3.zero;
				transform.localRotation = Quaternion.identity;
			}
			else
			{
				// Place in hand.
				Transform parent = left ? LeftHand : RightHand;
				(Vector3 pos, Quaternion rot) orientation = GetHandSlotOrientation(left, true);
				transform.SetParent(parent);
				transform.localPosition = orientation.pos;
				transform.localRotation = orientation.rot;
			}
		}

		/// <summary>
		/// Retrieve the position and rotation of a hand slot.
		/// </summary>
		/// <param name="left">Whether to retrieve for the left (true) or right hand (false).</param>
		/// <param name="local">Whether to retrieve the orientation in local space relative to the hand (true) or global space (false).</param>
		/// <returns>An orientation tuple (position, rotation) of the <paramref name="left"/> hand's slot in <paramref name="local"/> space.</returns>
		public (Vector3 pos, Quaternion rot) GetHandSlotOrientation(bool left, bool local)
		{
			// TODO: Could possibly be cached in local space and then simply converted for global.

			// Calculate position.
			Transform hand = left ? LeftHand : RightHand;
			Vector3 handPos = hand.position;
			Vector3 middleFPos = lookup.Lookup(left ? HumanBoneIdentifiers.LEFT_MIDDLE_PROXIMAL : HumanBoneIdentifiers.RIGHT_MIDDLE_PROXIMAL).position;
			Vector3 position = Vector3.Lerp(handPos, middleFPos, 0.8f);

			// Calculate rotation.
			Vector3 thumb = lookup.Lookup(left ? HumanBoneIdentifiers.LEFT_THUMB_PROXIMAL : HumanBoneIdentifiers.RIGHT_THUMB_PROXIMAL).position;
			Vector3 handToMiddleF = Vector3.Normalize(middleFPos - handPos);
			Vector3 handToThumb = Vector3.Normalize(thumb - handPos);
			Quaternion rotation = Quaternion.LookRotation(handToThumb, -handToMiddleF);

			Vector3 offset = left ? handSlotPointOffset.MirrorX() : handSlotPointOffset;
			position += rotation * offset;

			if (local)
			{
				// Convert to local.
				position = hand.InverseTransformPoint(position);
				rotation = Quaternion.Inverse(hand.rotation) * rotation;
			}

			return (position, rotation);
		}

		private void OnEquip(bool isLeft, RuntimeEquipedData data)
		{
			// Parent and position.
			if (data.EquipedInstance != null)
			{
				SetParentAndOrientation(data.EquipedInstance.transform, isLeft, Sheathed);
			}

			if (isLeft)
			{
				leftEquip = data;
			}
			else
			{
				rightEquip = data;
			}
		}

		private void OnUnequip(bool isLeft, RuntimeEquipedData data)
		{
			if (isLeft)
			{
				leftEquip = null;
			}
			else
			{
				rightEquip = null;
			}
		}

		#region Gizmos

		protected void OnDrawGizmos()
		{
			if (drawGizmos)
			{
				DrawHandSlotGizmos();
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
