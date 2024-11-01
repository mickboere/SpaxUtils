using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledDataCollection
	{
		public int Count => BoolCount + FloatCount;
		public int BoolCount => BoolData == null ? 0 : BoolData.Count;
		public int FloatCount => FloatData == null ? 0 : FloatData.Count;

		public List<LabeledBoolData> BoolData => boolData;
		public List<LabeledFloatData> FloatData => floatData;
		public List<LabeledIntData> IntData => intData;

		[SerializeField] private List<LabeledBoolData> boolData;
		[SerializeField] private List<LabeledFloatData> floatData;
		[SerializeField] private List<LabeledIntData> intData;

		/// <summary>
		/// Convert this labeled data collection to a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="id">The identifier to give to the data collection. If null, a new Guid will be generated.</param>
		public RuntimeDataCollection ToRuntimeDataCollection(string id = null)
		{
			return ApplyToRuntimeDataCollection(new RuntimeDataCollection(id ?? Guid.NewGuid().ToString()), true);
		}

		/// <summary>
		/// Applies all stored labeled data to <paramref name="runtimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection ApplyToRuntimeDataCollection(RuntimeDataCollection runtimeDataCollection, bool overwrite = true)
		{
			// Bools
			foreach (LabeledBoolData b in boolData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(b.ID) != null)
				{
					runtimeDataCollection.SetValue(b.ID, b.BoolValue);
				}
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(f.ID) != null)
				{
					runtimeDataCollection.SetValue(f.ID, f.FloatValue);
				}
			}

			return runtimeDataCollection;
		}

		/// <summary>
		/// Applies all stored labeled data to <paramref name="entity"/>.
		/// </summary>
		public void ApplyToEntity(IEntity entity, bool overwrite = true)
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
