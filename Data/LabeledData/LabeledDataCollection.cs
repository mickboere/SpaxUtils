using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class LabeledDataCollection
	{
		public int Count => BoolCount + FloatCount + IntCount + StringCount;
		public int BoolCount => BoolData == null ? 0 : BoolData.Count;
		public int FloatCount => FloatData == null ? 0 : FloatData.Count;
		public int IntCount => IntData == null ? 0 : IntData.Count;
		public int StringCount => StringData == null ? 0 : StringData.Count;

		public List<LabeledBoolData> BoolData => boolData;
		public List<LabeledFloatData> FloatData => floatData;
		public List<LabeledIntData> IntData => intData;
		public List<LabeledStringData> StringData => stringData;

		[SerializeField] private List<LabeledBoolData> boolData;
		[SerializeField] private List<LabeledFloatData> floatData;
		[SerializeField] private List<LabeledIntData> intData;
		[SerializeField] private List<LabeledStringData> stringData;

		/// <summary>
		/// Convert this labeled data collection to a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="id">The identifier to give to the data collection. If null, a new Guid will be generated.</param>
		public RuntimeDataCollection ToRuntimeDataCollection(string id = null)
		{
			RuntimeDataCollection collection = new RuntimeDataCollection(id.IsNullOrEmpty() ? Guid.NewGuid().ToString() : id);
			return ApplyToRuntimeDataCollection(collection, true);
		}

		/// <summary>
		/// Applies all stored labeled data to <paramref name="runtimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection ApplyToRuntimeDataCollection(RuntimeDataCollection runtimeDataCollection, bool overwrite)
		{
			// Bools
			foreach (LabeledBoolData b in boolData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(b.ID) == null)
				{
					runtimeDataCollection.SetValue(b.ID, b.BoolValue);
				}
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(f.ID) == null)
				{
					runtimeDataCollection.SetValue(f.ID, f.FloatValue);
				}
			}

			// Ints
			foreach (LabeledIntData i in intData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(i.ID) == null)
				{
					runtimeDataCollection.SetValue(i.ID, i.IntValue);
				}
			}

			// Strings
			foreach (LabeledStringData s in stringData)
			{
				if (overwrite || runtimeDataCollection.GetEntry(s.ID) == null)
				{
					runtimeDataCollection.SetValue(s.ID, s.StringValue);
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
				if (overwrite || entity.GetDataValue(b.ID) == null)
				{
					entity.SetDataValue(b.ID, b.BoolValue);
				}
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				if (overwrite || entity.GetDataValue(f.ID) == null)
				{
					entity.SetDataValue(f.ID, f.FloatValue);
				}
			}

			// Ints
			foreach (LabeledIntData i in intData)
			{
				if (overwrite || entity.GetDataValue(i.ID) == null)
				{
					entity.SetDataValue(i.ID, i.IntValue);
				}
			}

			// Strings
			foreach (LabeledStringData s in stringData)
			{
				if (overwrite || entity.GetDataValue(s.ID) == null)
				{
					entity.SetDataValue(s.ID, s.StringValue);
				}
			}
		}

		/// <summary>
		/// Returns whether <paramref name="collection"/> contains data that is dentical to this labeled data collection.
		/// </summary>
		public bool Matches(RuntimeDataCollection collection)
		{
			// Bools
			foreach (LabeledBoolData b in boolData)
			{
				if (!collection.TryGetValue(b.ID, out bool v) || v != b.BoolValue)
				{
					return false;
				}
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				if (!collection.TryGetValue(f.ID, out float v) || !v.Approx(f.FloatValue))
				{
					return false;
				}
			}

			// Ints
			foreach (LabeledIntData i in intData)
			{
				if (!collection.TryGetValue(i.ID, out int v) || v != i.IntValue)
				{
					return false;
				}
			}

			// Strings
			foreach (LabeledStringData s in stringData)
			{
				if (!collection.TryGetValue(s.ID, out string v) || v != s.StringValue)
				{
					return false;
				}
			}

			return true;
		}
	}
}
