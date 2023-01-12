using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public struct ArmedSettings
	{
		[Header("Position")]
		[SerializeField] public float smoothTime;
		[SerializeField] public float width;
		[SerializeField] public float height;
		[SerializeField] public float forward;
		[SerializeField] public float handInfluence;
		[SerializeField] public float accelerationInfluence;
		[SerializeField] public float maxAccelerationInfluence;
		[SerializeField] public float velocityInfluence;
		[SerializeField] public float maxVelocityInfluence;
		[Header("Rotation")]
		[SerializeField] public float rotationSmoothTime;
		[SerializeField] public float rotationVelocityInfluence;
		[SerializeField] public float maxRotationVelocityInfluence;
		[SerializeField] public float rotationInOut;
		[SerializeField] public float rotationUpDown;
		[SerializeField, Range(0f, 1f)] public float elbowHintWeight;
	}
}
