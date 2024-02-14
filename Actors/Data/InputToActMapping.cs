﻿using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class InputToActMapping : IAct
	{
		public string ActionMap => actionMap;
		public string Input => input;
		public string Title => act;
		public bool Interuptable => interuptable;
		public bool Interuptor => interuptor;
		public float Buffer => customBuffer ? buffer : Act<bool>.DEFAULT_BUFFER;
		public bool HoldEveryFrame => holdEveryFrame;

		[SerializeField, ConstDropdown(typeof(IActConstants))] private string act;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string input;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string actionMap;
		[SerializeField] private bool interuptable;
		[SerializeField] private bool interuptor;
		[SerializeField, HideInInspector] private bool customBuffer;
		[SerializeField, Conditional(nameof(customBuffer), drawToggle: true)] private float buffer;
		[SerializeField] private bool holdEveryFrame;
	}
}