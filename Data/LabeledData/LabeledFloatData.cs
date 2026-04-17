using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledFloatData : ILabeledData
	{
		public string ID => identifier;
		public object Value { get { return value; } set { this.value = (float)value; } }
		public Type ValueType => typeof(float);
		public float FloatValue => value;

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers))] private string identifier;
		[SerializeField] private float value;

		public void Apply(RuntimeDataCollection runtimeDataCollection, bool overwrite, bool dirty)
		{
			if (overwrite || runtimeDataCollection.GetEntry(ID) == null)
			{
				runtimeDataCollection.SetValue(ID, FloatValue, true, dirty);
			}
		}
	}
}
