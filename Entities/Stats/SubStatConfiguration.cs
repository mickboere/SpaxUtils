using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[System.Serializable]
	public class SubStatConfiguration : ISubStatConfiguration
	{
		public string Identifier => identifier;
		public float DefaultValue => defaultValue;
		public bool HasMinValue => hasMinValue;
		public float MinValue => minValue;
		public bool HasMaxValue => hasMaxValue;
		public float MaxValue => maxValue;

		public string Name => string.IsNullOrWhiteSpace(name) ? Identifier.LastDivision().Replace('_', ' ') : name;
		public string Description => description;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string identifier;
		[SerializeField] private float defaultValue;
		[SerializeField, HideInInspector] private bool hasMinValue;
		[SerializeField, Conditional(nameof(hasMinValue), drawToggle: true)] private float minValue;
		[SerializeField, HideInInspector] private bool hasMaxValue;
		[SerializeField, Conditional(nameof(hasMaxValue), drawToggle: true)] private float maxValue;
		[SerializeField] private string name;
		[SerializeField, TextArea] private string description;
	}
}
