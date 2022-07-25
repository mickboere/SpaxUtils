using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledBoolData : ILabeledData
	{
		public string UID => identifier;
		public object Value { get { return value; } set { this.value = (bool)value; } }
		public bool BoolValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string identifier;
		[SerializeField] private bool value;
	}
}
