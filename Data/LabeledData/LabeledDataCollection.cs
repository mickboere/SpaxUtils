using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledDataCollection
	{
		public int Count => BoolData.Count + FloatData.Count;

		public List<LabeledBoolData> BoolData => boolData;
		public List<LabeledFloatData> FloatData => floatData;

		[SerializeField] private List<LabeledBoolData> boolData;
		[SerializeField] private List<LabeledFloatData> floatData;

		/// <summary>
		/// Convert this labeled data collection to a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="id">The desired identifier to give to the data collection.</param>
		public RuntimeDataCollection ToRuntimeDataCollection(string id)
		{
			return ApplyToRuntimeDataCollection(new RuntimeDataCollection(id), true);
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
