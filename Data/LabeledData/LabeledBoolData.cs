using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledBoolData : ILabeledData
	{
		public string ID => identifier;
		public object Value { get { return value; } set { this.value = (bool)value; } }
		public Type ValueType => typeof(bool);
		public bool BoolValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string identifier;
		[SerializeField] private bool value;
	}
}
