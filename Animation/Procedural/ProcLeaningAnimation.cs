using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ProcLeaningAnimation : EntityComponentBase
	{
		private float Delta => frameRate > 0 ? customDelta : Time.fixedDeltaTime;

		[SerializeField] private float sensitivity = 30f;
		[SerializeField] private float speed = 6f;
		[SerializeField] private float maxAngle = 20f;
		[SerializeField] private int frameRate;

		private RigidbodyWrapper wrapper;
		private CallbackService callbackService;
		private float customDelta;

		public void InjectDependencies(RigidbodyWrapper wrapper, CallbackService callbackService)
		{
			this.wrapper = wrapper;
			this.callbackService = callbackService;
		}

		protected void OnEnable()
		{
			if (frameRate > 0)
			{
				customDelta = 1f / frameRate;
				callbackService.AddCustom(this, 1f / frameRate, UpdateRotation);
			}
		}

		protected void OnDisable()
		{
			if (callbackService != null)
			{
				callbackService.RemoveCustom(this);
			}
		}

		protected void FixedUpdate()
		{
			if (frameRate <= 0)
			{
				UpdateRotation();
			}
		}

		private void UpdateRotation()
		{
			Vector3 dir = wrapper.RelativeAcceleration * sensitivity;
			Quaternion target = Quaternion.identity;
			if (dir.magnitude > 0.01f)
			{
				target = Quaternion.Euler(new Vector3(dir.z, 0f, -dir.x)).Clamp(Vector3.up, wrapper.transform.up, maxAngle);
			}
			transform.localRotation = Quaternion.Lerp(transform.localRotation, target, speed * Delta);
		}
	}
}
