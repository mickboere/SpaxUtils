using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledStringData : ILabeledData
	{
		public string ID => identifier;
		public object Value { get { return value; } set { this.value = (string)value; } }
		public Type ValueType => typeof(string);
		public string StringValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), inputField: true)] private string identifier;
		[SerializeField] private string value;
	}
}
