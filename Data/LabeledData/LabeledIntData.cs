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

		[SerializeField, ConstDropdown(typeof(ILabeledDataIdentifiers), inputField: true)] private string identifier;
		[SerializeField] private int value;

		public void Apply(RuntimeDataCollection runtimeDataCollection, bool overwrite, bool dirty)
		{
			if (overwrite || runtimeDataCollection.GetEntry(ID) == null)
			{
				runtimeDataCollection.SetValue(ID, IntValue, true, dirty);
			}
		}
	}
}
