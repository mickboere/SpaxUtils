using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledIntData : ILabeledData
	{
		public string ID => identifier;
		public object Value { get { return value; } set { this.value = (int)value; } }
		public Type ValueType => typeof(int);
		public int IntValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string identifier;
		[SerializeField] private int value;
	}
}
