﻿using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[Serializable]
	public class InputToActMapping : IAct
	{
		public string Title => act;
		public bool Interuptable => interuptable;
		public bool Interuptor => interuptor;
		public float Buffer => hasCustomBuffer ? customBuffer : Act<bool>.DEFAULT_BUFFER;

		public string ActionMap => actionMap;
		public string Input => input;
		public bool EatInput => eatInput;
		public int InputPrio => inputPrio;
		public bool HoldEveryFrame => holdEveryFrame;

		[SerializeField, ConstDropdown(typeof(IActTitles))] private string act;
		[SerializeField, ConstDropdown(typeof(IInputActions))] private string input;
		[SerializeField, ConstDropdown(typeof(IInputActionMaps))] private string actionMap;
		[SerializeField] private bool eatInput;
		[SerializeField] private int inputPrio;
		[SerializeField] private bool interuptable;
		[SerializeField] private bool interuptor;
		[SerializeField, HideInInspector] private bool hasCustomBuffer;
		[SerializeField, Conditional(nameof(hasCustomBuffer), drawToggle: true)] private float customBuffer;
		[SerializeField] private bool holdEveryFrame;
	}
}
