using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class ImpactSFXData
	{
		[field: SerializeField, Range(0f, 1f)] public float Intensity { get; private set; }
		[field: SerializeField] public SFXData SFX { get; private set; }
	}
}
