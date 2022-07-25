using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledFloatData : ILabeledData
	{
		public string UID => identifier;
		public object Value { get { return value; } set { this.value = (float)value; } }
		public float FloatValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifierConstants))] private string identifier;
		[SerializeField] private float value;
	}
}
