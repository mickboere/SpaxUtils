using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public struct ArmedSettings
	{
		[Header("Position")]
		[SerializeField] public float smoothTime;
		[SerializeField] public float broadness;
		[SerializeField] public float height;
		[SerializeField] public float forward;
		[SerializeField] public float handInfluence;
		[SerializeField] public float accelerationInfluence;
		[SerializeField] public float velocityInfluence;
		[Header("Rotation")]
		[SerializeField] public float rotationSmoothTime;
		[SerializeField] public float rotationVelocityInfluence;
	}
}
