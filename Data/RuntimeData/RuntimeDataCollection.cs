using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// <see cref="RuntimeDataEntry"/> collection, also implements <see cref="RuntimeDataEntry"/> to allow for nested collections.
	/// </summary>
	[Serializable]
	public class RuntimeDataCollection : RuntimeDataEntry, IDisposable
	{
		/// <summary>
		/// Invoked when any <see cref="RuntimeDataEntry"/> value is updated through this <see cref="RuntimeDataCollection"/>.
		/// </summary>
		public event Action<RuntimeDataEntry> DataUpdatedEvent;

		/// <summary>
		/// The <see cref="Data"/> as generic value object, required for implementing <see cref="ILabeledData"/>.
		/// Can be set using any <see cref="IEnumerable{T}"/> where T is <see cref="RuntimeDataEntry"/>.
		/// </summary>
		public override object Value
		{
			get { return Data; }
			set
			{
				if (value is JToken token)
				{
					Data = token.ToObject<List<RuntimeDataEntry>>();
				}
				else
				{
					Data = new List<RuntimeDataEntry>((IEnumerable<RuntimeDataEntry>)value);
				}
			}
		}

		/// <summary>
		/// All data entries making up this runtime data.
		/// </summary>
		[JsonIgnore]
		public IReadOnlyList<RuntimeDataEntry> Data
		{
			get { return _data.Values.ToList(); }
			set
			{
				_data = new Dictionary<string, RuntimeDataEntry>();
				foreach (RuntimeDataEntry entry in value)
				{
					_data[entry.ID] = entry;
					entry.Parent = this;
				}
			}
		}
		private Dictionary<string, RuntimeDataEntry> _data;

		public RuntimeDataCollection(string id, List<RuntimeDataEntry> dataEntries = null, RuntimeDataCollection parent = null) : base(id, null, parent)
		{
			if (dataEntries != null)
			{
				Data = dataEntries;
			}
			else
			{
				_data = new Dictionary<string, RuntimeDataEntry>();
			}
		}

		public override void Dispose()
		{
			foreach (KeyValuePair<string, RuntimeDataEntry> entry in _data)
			{
				entry.Value.Dispose();
			}
			base.Dispose();
		}

		/// <summary>
		/// Returns a new <see cref="RuntimeDataCollection"/> instance with a random <see cref="Guid"/> ID.
		/// </summary>
		/// <param name="dataEntries">The <see cref="RuntimeDataEntry"/>s to add.</param>
		/// <returns>A new <see cref="RuntimeDataCollection"/> instance with a random <see cref="Guid"/> ID.</returns>
		public static RuntimeDataCollection New(params RuntimeDataEntry[] dataEntries)
		{
			RuntimeDataCollection data = new RuntimeDataCollection(
				Guid.NewGuid().ToString(),
				dataEntries == null ? null : new List<RuntimeDataEntry>(dataEntries));
			return data;
		}

		/// <summary>
		/// Returns whether the collection contains an entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID to check for.</param>
		/// <returns>Whether the collection contains an entry with ID <paramref name="id"/>.</returns>
		public bool ContainsEntry(string id)
		{
			return _data.ContainsKey(id);
		}

		/// <summary>
		/// Will attempt to add the <paramref name="entry"/> to the data collection.
		/// </summary>
		/// <param name="entry">The entry to attempt to add.</param>
		/// <param name="overwriteExisting">Should existing data be overriden if its ID matches the <paramref name="entry"/>'s?</param>
		/// <returns>TRUE if adding/overwriting was successfull, FALSE if it wasn't.</returns>
		public bool TryAdd(RuntimeDataEntry entry, bool overwriteExisting = false)
		{
			RuntimeDataEntry present = GetEntry(entry.ID);
			if (present != null && !overwriteExisting)
			{
				// No need to log as it is a TRY add.
				return false;
			}

			_data[entry.ID] = entry;
			entry.Parent = this;
			DataUpdatedEvent?.Invoke(entry);
			return true;
		}

		/// <summary>
		/// Removes <see cref="RuntimeDataEntry"/> with <paramref name="id"/>, if any.
		/// </summary>
		/// <param name="id">The identifier of the <see cref="RuntimeDataEntry"/> to remove.</param>
		/// <param name="dispose">Should the <see cref="RuntimeDataEntry"/> be disposed after removal?</param>
		public bool TryRemove(string id, bool dispose = false)
		{
			RuntimeDataEntry present = GetEntry(id);

			if (present == null)
			{
				return false;
			}

			present.Parent = null;

			if (dispose)
			{
				present.Dispose();
			}

			return true;
		}

		/// <summary>
		/// Sets the data entry with ID <paramref name="id"/>'s value to <paramref name="value"/>.
		/// </summary>
		/// <param name="id">The id of data entry we're looking to replace.</param>
		/// <param name="value">The value to set in the data entry of type <paramref name="id"/>.</param>
		/// <param name="createIfNull">If the desired data entry does not exist yet, should it be created?</param>
		public void Set(string id, object value, bool createIfNull = true)
		{
			RuntimeDataEntry entry = GetEntry(id);
			if (entry != null)
			{
				entry.Value = value;
				DataUpdatedEvent?.Invoke(entry);
			}
			else if (createIfNull)
			{
				entry = new RuntimeDataEntry(id, value, this);
				_data.Add(entry.ID, entry);
				DataUpdatedEvent?.Invoke(entry);
			}
		}

		#region Getting Methods

		/// <summary>
		/// Returns entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the entry to retrieve.</param>
		/// <returns>Entry with ID <paramref name="id"/>, NULL if null.</returns>
		public RuntimeDataEntry GetEntry(string id)
		{
			if (_data.ContainsKey(id))
			{
				return _data[id];
			}
			return null;
		}

		/// <summary>
		/// Returns entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the entry to retrieve.</param>
		/// <returns>Entry with ID <paramref name="id"/>, NULL if null.</returns>
		public T GetEntry<T>(string id, T defaultIfNull = null) where T : RuntimeDataEntry
		{
			RuntimeDataEntry entry = GetEntry(id);

			if (entry == null)
			{
				if (defaultIfNull != null)
				{
					if (TryAdd(defaultIfNull))
					{
						return defaultIfNull;
					}
					else
					{
						SpaxDebug.Error($"Default entry could not be added.", $"For ID '{id}', default: {defaultIfNull}");
					}
				}
			}
			else
			{
				if (entry is T cast)
				{
					return cast;
				}
				else if (typeof(T).IsAssignableFrom(typeof(RuntimeDataCollection)) && entry.Value is JArray array)
				{
					// Entry is supposed to be a collection, convert and replace it.
					List<RuntimeDataEntry> entries = array.ToObject<List<RuntimeDataEntry>>();
					if (entries != null)
					{
						RuntimeDataCollection collection = new RuntimeDataCollection(id, entries, this);
						_data[id] = collection;
						entry.Dispose();
						DataUpdatedEvent?.Invoke(collection);
						return collection as T;
					}
				}

				SpaxDebug.Error($"Entry cast is not valid.", $"For ID '{id}', cannot cast '{entry.GetType().FullName}' to {typeof(T).FullName}.\n" +
					$"Entry value: {(entry.Value == null ? "null" : entry.Value.GetType().FullName)}");
			}

			return null;
		}

		/// <summary>
		/// Returns all entries of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The types of entry to return.</typeparam>
		/// <returns>All entries of type <typeparamref name="T"/>.</returns>
		public List<T> GetEntries<T>() where T : RuntimeDataEntry
		{
			List<T> results = new List<T>();

			bool isCollection = typeof(T).IsAssignableFrom(typeof(RuntimeDataCollection));
			List<RuntimeDataEntry> entries = _data.Values.ToList();

			foreach (RuntimeDataEntry entry in entries)
			{
				if (entry is T cast)
				{
					results.Add(cast);
				}
				else if (isCollection && entry.Value is JArray array)
				{
					List<RuntimeDataEntry> childEntries = array.ToObject<List<RuntimeDataEntry>>();
					if (childEntries != null)
					{
						// Entry is supposed to be a collection, convert and replace it.
						RuntimeDataCollection collection = new RuntimeDataCollection(entry.ID, childEntries, this);
						_data[entry.ID] = collection;
						entry.Dispose();
						DataUpdatedEvent?.Invoke(collection);
						results.Add(collection as T);
					}
					else
					{
						SpaxDebug.Error($"Entry cast is not valid.", $"For ID '{entry.ID}', cannot cast '{entry.GetType().FullName}' to {typeof(T).FullName}.\n" +
							$"Entry value: {(entry.Value == null ? "null" : entry.Value.GetType().FullName)}");
					}
				}
			}

			return results;
		}

		/// <summary>
		/// Returns value of entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the entry we want to retrieve its value of.</param>
		/// <returns>Value of entry with ID <paramref name="id"/>, NULL if entry is null.</returns>
		public object Get(string id)
		{
			RuntimeDataEntry entry = GetEntry(id);
			if (entry != null)
			{
				return entry.Value;
			}
			return null;
		}

		/// <summary>
		/// Returns value of entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the entry we want to retrieve its value of.</param>
		/// <typeparam name="T">The type to cast the result to.</typeparam>
		/// <returns>Value of entry with ID <paramref name="id"/> as <typeparamref name="T"/>, DEFAULT if entry is null.</returns>
		public T Get<T>(string id)
		{
			RuntimeDataEntry entry = GetEntry(id);
			if (entry != null)
			{
				if (entry.Value is T cast)
				{
					return cast;
				}
				else if (entry.Value is JToken token)
				{
					// Object was deserialized as JToken, try to convert it to desired type.
					T jCast = token.ToObject<T>();
					if (jCast != null)
					{
						// Replace entry value with cast since we now know its correct type.
						entry.Value = jCast;
						return jCast;
					}
				}

				SpaxDebug.Error($"Value cast is not valid.", $"For ID '{id}', cannot cast '{entry.Value.GetType().FullName}' to {typeof(T).FullName}");
			}

			return default;
		}

		/// <summary>
		/// Returns value of entry with ID <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The ID of the entry we want to retrieve its value of.</param>
		/// <typeparam name="T">The type to cast the result to.</typeparam>
		/// <param name="value">Value of entry with ID <paramref name="id"/> as <typeparamref name="T"/>, DEFAULT if entry is null.</param>
		/// <returns>True when data with ID <paramref name="id"/> was found, false if it wasn't.</returns>
		public bool TryGet<T>(string id, out T value)
		{
			RuntimeDataEntry entry = GetEntry(id);
			if (entry != null)
			{
				value = (T)entry.Value;
				return true;
			}
			value = default;
			return false;
		}

		#endregion

		#region Cloning and Appending

		/// <summary>
		/// Create a deep copy of this <see cref="RuntimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection Clone()
		{
			RuntimeDataCollection collection = new RuntimeDataCollection(ID);

			if (_data == null)
			{
				SpaxDebug.Error($"Data is null, this should not be possible.", $"ID={ID}, Parent={(Parent == null ? "null" : Parent.ID)}");
				return collection;
			}

			foreach (KeyValuePair<string, RuntimeDataEntry> entry in _data)
			{
				// If entry is collection, clone and add.
				if (entry.Value is RuntimeDataCollection childCollection)
				{
					collection.TryAdd(childCollection.Clone(), true);
				}
				else // If entry is not a collection, 
				{
					collection.TryAdd(new RuntimeDataEntry(entry.Value, collection), true);
				}
			}

			return collection;
		}

		/// <summary>
		/// Will copy all <see cref="RuntimeDataEntry"/>s from <paramref name="runtimeDataCollection"/> into this collection.
		/// </summary>
		/// <param name="runtimeDataCollection">The collection to copy the entries from.</param>
		/// <param name="overwriteExisting">If this collection already contains an entry with the same ID, should it be overwritten?</param>
		public RuntimeDataCollection Append(RuntimeDataCollection runtimeDataCollection, bool overwriteExisting = false)
		{
			foreach (KeyValuePair<string, RuntimeDataEntry> entry in runtimeDataCollection._data)
			{
				if (_data.ContainsKey(entry.Key) && !overwriteExisting)
				{
					continue;
				}

				_data[entry.Key] = new RuntimeDataEntry(entry.Value, this);
			}

			return this;
		}

		/// <summary>
		/// Will copy all <see cref="RuntimeDataEntry"/>s from all <paramref name="collections"/> into this collection.
		/// In case of duplicate ID's, the entry already in this collection will take precedence.
		/// </summary>
		/// <param name="collections">The collections to copy the entries from.</param>
		public RuntimeDataCollection Append(params RuntimeDataCollection[] collections)
		{
			foreach (RuntimeDataCollection collection in collections)
			{
				Append(collection);
			}
			return this;
		}

		#endregion

		public override string ToString()
		{
			// TODO: JSON.
			return $"{{\n\tID={ID};\n\tData\n\t{{\n\t\t{string.Join(";\n\t\t", Data)}\n\t}}\n}}";
		}
	}
}
