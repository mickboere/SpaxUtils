using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ProcLeaningAnimation : EntityComponentMono
	{
		[SerializeField] private float sensitivity = 30f;
		[SerializeField] private float speed = 6f;
		[SerializeField] private float maxAngle = 20f;
		[SerializeField] private UpdateMode updateMode;

		private RigidbodyWrapper wrapper;
		private CallbackService callbackService;
		private GrounderComponent grounder;

		public void InjectDependencies(RigidbodyWrapper wrapper, CallbackService callbackService, GrounderComponent grounder)
		{
			this.wrapper = wrapper;
			this.callbackService = callbackService;
			this.grounder = grounder;
		}

		protected void OnEnable()
		{
			callbackService.SubscribeUpdate(updateMode, this, UpdateRotation);
		}

		protected void OnDisable()
		{
			if (callbackService != null)
			{
				callbackService.RemoveCustom(this);
				callbackService.UnsubscribeUpdates(this);
			}
		}

		private void UpdateRotation(float delta)
		{
			Vector3 dir = wrapper.RelativeAcceleration * sensitivity;
			Quaternion target = Quaternion.identity;
			if (grounder.Grounded && dir.magnitude > 0.01f)
			{
				target = Quaternion.Euler(new Vector3(dir.z, 0f, -dir.x)).Clamp(Vector3.up, wrapper.transform.up, maxAngle);
			}
			transform.localRotation = Quaternion.Lerp(transform.localRotation, target, speed * delta * EntityTimeScale);
		}
	}
}
