using System;
using UnityEngine;

namespace SpaxUtils.UI
{
	[Serializable]
	public class UIScreenContextConfig
	{
		[SerializeField, ConstDropdown(typeof(IContextIdentifiers))] public string context;
		[SerializeField] public UIScreen screen;
	}
}
