using System;
using UnityEngine;

namespace SpaxUtils
{
	public class AutoHandSlotProviderComponent : EntityComponentBase
	{
		protected Transform LeftHand => transformLookup.Lookup(HumanBoneIdentifiers.LEFT_HAND);
		protected Transform RightHand => transformLookup.Lookup(HumanBoneIdentifiers.RIGHT_HAND);

		[SerializeField] private bool left;
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string leftType;
		[SerializeField] private bool right;
		[SerializeField, ConstDropdown(typeof(IEquipmentSlotTypeConstants))] private string rightType;
		[SerializeField] private Vector3 handSlotPointOffset = new Vector3(-0.01f, 0f, 0f);
		[SerializeField] private bool drawGizmos;

		private TransformLookup transformLookup;
		private IEquipmentComponent equipmentComponent;

		public void InjectDependencies(TransformLookup transformLookup, IEquipmentComponent equipmentComponent)
		{
			this.transformLookup = transformLookup;
			this.equipmentComponent = equipmentComponent;
		}

		protected void Awake()
		{
			if (left)
			{
				equipmentComponent.AddSlot(new EquipmentSlot(HumanBoneIdentifiers.LEFT_HAND, leftType, LeftHand, () => GetHandSlotPoint(true, true)));
			}

			if (right)
			{
				equipmentComponent.AddSlot(new EquipmentSlot(HumanBoneIdentifiers.RIGHT_HAND, rightType, RightHand, () => GetHandSlotPoint(false, true)));
			}
		}

		protected void OnDestroy()
		{
			equipmentComponent.RemoveSlot(HumanBoneIdentifiers.LEFT_HAND);
			equipmentComponent.RemoveSlot(HumanBoneIdentifiers.RIGHT_HAND);
		}

		/// <summary>
		/// Humanoid-only method to retrieve the (perfect) hand equipment position and rotation.
		/// </summary>
		/// <param name="isLeft">True will return position and rotation for left hand, false for right.</param>
		/// <returns>Position and rotation where to place equipment that is supposed to be held by the agent's hand.</returns>
		private (Vector3 pos, Quaternion rot) GetHandSlotPoint(bool isLeft, bool local)
		{
			Transform hand = isLeft ? LeftHand : RightHand;
			Vector3 handPos = hand.position;
			Vector3 fingerPos = transformLookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_MIDDLE_PROXIMAL : HumanBoneIdentifiers.RIGHT_MIDDLE_PROXIMAL).position;
			Vector3 position = Vector3.Lerp(handPos, fingerPos, 0.8f);

			Vector3 thumb = transformLookup.Lookup(isLeft ? HumanBoneIdentifiers.LEFT_THUMB_PROXIMAL : HumanBoneIdentifiers.RIGHT_THUMB_PROXIMAL).position;
			Vector3 handToFinger = Vector3.Normalize(fingerPos - handPos);
			Vector3 handToThumb = Vector3.Normalize(thumb - handPos);
			Quaternion rotation = Quaternion.LookRotation(handToThumb, -handToFinger);

			Vector3 offset = isLeft ? handSlotPointOffset.MirrorX() : handSlotPointOffset;
			position += rotation * offset;

			if (local)
			{
				// Convert to local.
				position = hand.InverseTransformDirection(position);
				rotation *= Quaternion.Inverse(hand.rotation);
			}

			return (position, rotation);
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
			if (transformLookup == null)
			{
				transformLookup = gameObject.GetComponent<TransformLookup>();
			}
			if (transformLookup != null)
			{
				Draw(true);
				Draw(false);

				void Draw(bool isLeft)
				{
					(Vector3 pos, Quaternion rot) orientation = GetHandSlotPoint(isLeft, false);
					Gizmos.color = Color.yellow;
					Gizmos.DrawSphere(orientation.pos, 0.02f);
					Gizmos.color = Color.blue;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.forward);
					Gizmos.color = Color.red;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * (isLeft ? Vector3.left : Vector3.right));
					Gizmos.color = Color.green;
					Gizmos.DrawLine(orientation.pos, orientation.pos + orientation.rot * Vector3.up);
				}
			}
		}

		#endregion
	}
}
