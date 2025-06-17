using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class ShakeSettings
	{
		[field: SerializeField] public Vector3 Magnitude { get; private set; }
		[field: SerializeField] public float Frequency { get; private set; }
		[field: SerializeField] public float Duration { get; private set; }
		[field: SerializeField] public AnimationCurve Falloff { get; private set; }
	}
}
