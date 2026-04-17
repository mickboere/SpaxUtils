using System;
using UnityEngine;


namespace SpaxUtils
{
	[Serializable]
	public class ActionSFXData
	{
		[field: SerializeField, ConstDropdown(typeof(IActIdentifiers))] public string Act { get; private set; }
		[field: SerializeField] public SFXData SFX { get; private set; }
	}
}
