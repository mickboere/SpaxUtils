using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class ImpactSFXData
	{
		[field: SerializeField] public float Force { get; private set; }
		[field: SerializeField] public SFXData SFX { get; private set; }
	}
}
