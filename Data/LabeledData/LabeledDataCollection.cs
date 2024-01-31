using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledDataCollection
	{
		[SerializeField] private List<LabeledBoolData> boolData;
		[SerializeField] private List<LabeledFloatData> floatData;

		public void ApplyToEntity(IEntity entity, bool overwrite)
		{
			// Bools
			foreach (LabeledBoolData b in boolData)
			{
				if (overwrite || entity.GetDataValue(b.ID) != null)
				{
					entity.SetDataValue(b.ID, b.BoolValue);
				}
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				if (overwrite || entity.GetDataValue(f.ID) != null)
				{
					entity.SetDataValue(f.ID, f.FloatValue);
				}
			}
		}
	}
}
