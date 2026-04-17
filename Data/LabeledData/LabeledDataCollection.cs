using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Represents a collection of labeled data, including booleans, floats, integers, strings, and octad data.
	/// </summary>
	/// <remarks>This class provides functionality to manage and manipulate labeled data, including converting it to
	/// a RuntimeDataCollection, applying its data to an existing runtime collection, and comparing  its contents with
	/// another collection. The collection supports multiple data types, each stored in separate lists, and provides
	/// properties to access the counts and data for each type.</remarks>
	[Serializable]
	public class LabeledDataCollection
	{
		public int Count => BoolCount + FloatCount + IntCount + StringCount + Vector8Count;
		public int BoolCount => BoolData == null ? 0 : BoolData.Count;
		public int FloatCount => FloatData == null ? 0 : FloatData.Count;
		public int IntCount => IntData == null ? 0 : IntData.Count;
		public int StringCount => StringData == null ? 0 : StringData.Count;
		public int Vector8Count => Vector8Data == null ? 0 : Vector8Data.Count;

		public List<LabeledBoolData> BoolData => boolData;
		public List<LabeledFloatData> FloatData => floatData;
		public List<LabeledIntData> IntData => intData;
		public List<LabeledStringData> StringData => stringData;
		public List<LabeledOctadData> Vector8Data => octadData;

		[SerializeField] private List<LabeledBoolData> boolData;
		[SerializeField] private List<LabeledFloatData> floatData;
		[SerializeField] private List<LabeledIntData> intData;
		[SerializeField] private List<LabeledStringData> stringData;
		[SerializeField] private List<LabeledOctadData> octadData;

		/// <summary>
		/// Convert this labeled data collection to a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="id">The identifier to give to the data collection. If null, a new Guid will be generated.</param>
		public RuntimeDataCollection ToRuntimeDataCollection(string id = null, bool dirty = false)
		{
			RuntimeDataCollection collection = new RuntimeDataCollection(id.IsNullOrEmpty() ? Guid.NewGuid().ToString() : id);
			ApplyToRuntimeDataCollection(collection, true, dirty);
			return collection;
		}

		/// <summary>
		/// Applies all stored labeled data to <paramref name="runtimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection ApplyToRuntimeDataCollection(RuntimeDataCollection runtimeDataCollection, bool overwrite, bool dirty)
		{
			// Bools
			foreach (LabeledBoolData b in boolData)
			{
				b.Apply(runtimeDataCollection, overwrite, dirty);
			}

			// Floats
			foreach (LabeledFloatData f in floatData)
			{
				f.Apply(runtimeDataCollection, overwrite, dirty);
			}

			// Ints
			foreach (LabeledIntData i in intData)
			{
				i.Apply(runtimeDataCollection, overwrite, dirty);
			}

			// Strings
			foreach (LabeledStringData s in stringData)
			{
				s.Apply(runtimeDataCollection, overwrite, dirty);
			}

			// Vector8s
			foreach (LabeledOctadData v in octadData)
			{
				v.Apply(runtimeDataCollection, overwrite, dirty);
			}

			return runtimeDataCollection;
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

			// Vector8s
			foreach (LabeledOctadData v in octadData)
			{
				for (int i = 0; i < 8; i++)
				{
					string id = v.StatOctad.GetIdentifier(i);
					if (!collection.TryGetValue(id, out float value) || !value.Approx(v.Vector8[i]))
					{
						return false;
					}
				}
			}

			return true;
		}
	}
}
