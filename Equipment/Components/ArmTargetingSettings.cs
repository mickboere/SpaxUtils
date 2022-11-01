using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public struct ArmTargetingSettings
	{
		[SerializeField] public float broadness;
		[SerializeField] public float handInfluence;
		[SerializeField] public float smoothTime;
		[SerializeField] public float accelerationInfluence;
		[SerializeField] public float velocityInfluence;
	}
}
