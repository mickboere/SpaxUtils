using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class ProcLeaningAnimation : EntityComponentBase
	{
		[SerializeField] private float sensitivity = 30f;
		[SerializeField] private float speed = 6f;
		[SerializeField] private float maxAngle = 20f;

		private RigidbodyWrapper wrapper;

		public void InjectDependencies(RigidbodyWrapper wrapper)
		{
			this.wrapper = wrapper;
		}

		protected void FixedUpdate()
		{
			Vector3 dir = wrapper.RelativeAcceleration * sensitivity;
			Quaternion target = Quaternion.identity;
			if (dir.magnitude > 0.01f)
			{
				target = Quaternion.Euler(new Vector3(dir.z, 0f, -dir.x)).Clamp(Vector3.up, wrapper.transform.up, maxAngle);
			}
			transform.localRotation = Quaternion.Lerp(transform.localRotation, target, speed * Time.fixedDeltaTime);
		}
	}
}
