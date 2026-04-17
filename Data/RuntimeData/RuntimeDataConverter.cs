using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpaxUtils
{
	public class RuntimeDataConverter : JsonConverter<RuntimeDataEntry>
	{
		public override bool CanRead => true;
		private readonly bool optimized;

		public RuntimeDataConverter(bool optimized = true)
		{
			this.optimized = optimized;
		}

		public override void WriteJson(JsonWriter writer, RuntimeDataEntry value, JsonSerializer serializer)
		{
			if (value == null ||
				(optimized && !value.Dirty) ||
				value.Value == null)
			{
				// Never write nulls, and (optionally) never write non-dirty entries.
				return;
			}

			if (value is RuntimeDataCollection collection)
			{
				// Never write empty collections (or collections that serialize to empty after filtering).
				List<RuntimeDataEntry> entriesToWrite = FilterEntriesToWrite(collection);
				if (entriesToWrite.Count == 0)
				{
					return;
				}

				// Write RuntimeDataCollection.
				writer.WriteStartObject();
				writer.WritePropertyName(collection.ID);
				writer.WriteStartArray();
				foreach (RuntimeDataEntry entry in entriesToWrite)
				{
					serializer.Serialize(writer, entry, typeof(RuntimeDataEntry));
				}
				writer.WriteEndArray();
				writer.WriteEndObject();
				return;
			}

			// Never write empty lists/arrays/enumerables; they're noise and cause type ambiguity on load.
			if (IsEmptyEnumerable(value.Value))
			{
				return;
			}

			// Write single RuntimeDataEntry (on single line).
			switch (value.Value)
			{
				case float f:
					// Important: invariant + round-trip ensures decimal dot and stable parsing across locales.
					writer.WriteRawValue("{\"" + value.ID + "\":\"" + f.ToString("R", CultureInfo.InvariantCulture) + "f\"}");
					break;

				case double d:
					// Important: invariant + round-trip ensures decimal dot and stable parsing across locales.
					writer.WriteRawValue("{\"" + value.ID + "\":\"" + d.ToString("R", CultureInfo.InvariantCulture) + "d\"}");
					break;

				case Vector3 v3:
					// Assumes ToParseableString produces a stable parseable format used by TryParseVector3.
					writer.WriteRawValue("{\"" + value.ID + "\":\"" + v3.ToParseableString() + "\"}");
					break;

				default:
					{
						// Use JToken to avoid accidentally serializing runtime objects in odd ways.
						JObject json = new JObject(new JProperty(value.ID, JToken.FromObject(value.Value)));
						writer.WriteRawValue(json.ToString(Formatting.None));
						break;
					}
			}
		}

		public override RuntimeDataEntry ReadJson(JsonReader reader, Type objectType, RuntimeDataEntry existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			JToken token = JToken.Load(reader);
			return GetEntry(token);
		}

		private RuntimeDataEntry GetEntry(JToken token)
		{
			if (token == null)
			{
				return null;
			}

			if (token is JValue)
			{
				SpaxDebug.Error("JValue cannot be converted to RuntimeDataEntry:", token.ToString());
				return null;
			}

			string id = ((JProperty)token.First).Name;
			JToken data = token[id];

			// Ignore nulls (noise / partial saves / older versions).
			if (data == null || data.Type == JTokenType.Null)
			{
				return null;
			}

			RuntimeDataEntry result = null;
			switch (data.Type)
			{
				case JTokenType.Array:
					result = FromArray(id, data as JArray);
					break;

				case JTokenType.Object:
					// Keep as plain object; if you need stronger typing later, do it at the call site.
					result = new RuntimeDataEntry(id, GetValue(data));
					break;

				case JTokenType.String:
					result = new RuntimeDataEntry(id, GetStringValue(data));
					break;

				case JTokenType.Integer:
					result = new RuntimeDataEntry(id, (int)data);
					break;

				case JTokenType.Boolean:
					result = new RuntimeDataEntry(id, (bool)data);
					break;

				case JTokenType.Float:
					// Newtonsoft already parsed a float/double in a locale-independent way here.
					result = new RuntimeDataEntry(id, (double)data);
					break;

				default:
					SpaxDebug.Error("Could not process entry data:", "tokenType=" + token.Type + ", id=" + id + ", dataTokenType=" + data.Type);
					result = new RuntimeDataEntry(id, data);
					break;
			}

			if (result != null)
			{
				result.Dirty = true; // All loaded data is dirty.
			}

			return result;
		}

		private RuntimeDataEntry FromArray(string id, JArray array)
		{
			if (array == null ||
				array.Count == 0)
			{
				// Ignore empty arrays entirely (noise).
				return null;
			}

			switch (array.First.Type)
			{
				case JTokenType.Object:
					{
						// RuntimeDataCollection
						List<RuntimeDataEntry> entries = new List<RuntimeDataEntry>();
						foreach (JToken child in array)
						{
							RuntimeDataEntry entry = GetEntry(child);
							if (entry != null)
							{
								entries.Add(entry);
							}
						}

						// Ignore collections that become empty after filtering.
						if (entries.Count == 0)
						{
							return null;
						}

						return new RuntimeDataCollection(id, entries);
					}

				case JTokenType.String:
					{
						// List<string>
						List<string> list = array.ToObject<List<string>>();
						if (list == null || list.Count == 0)
						{
							// Ignore empty lists entirely (noise).
							return null;
						}
						return new RuntimeDataEntry(id, list);
					}

				default:
					SpaxDebug.Error("(" + id + ") Unsuported array type: (" + array.First.Type + ")", array.ToString());
					return null;
			}
		}

		private object GetValue(JToken token)
		{
			// Important note:
			// token.ToObject<object>() can yield JObject/JArray/etc depending on content.
			// We keep that as-is; the caller decides how strongly to type it.
			return token.ToObject<object>();
		}

		private object GetStringValue(JToken token)
		{
			string s = (string)token;

			// Parse numeric and vector types from strings when they are explicitly tagged/parseable.
			// This is critical for stability because JSON number parsing can vary in precision/typing,
			// and we also want locale-invariant behavior (decimal dot).
			if (!string.IsNullOrEmpty(s) &&
				s.Length > 1 &&
				(char.IsDigit(s[0]) || s[0] == '-'))
			{
				if (s[s.Length - 1] == 'f')
				{
					// String is a float (written as "{value}f").
					string raw = s.TrimEnd('f');
					if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
					{
						return f;
					}
				}

				if (s[s.Length - 1] == 'd')
				{
					// String is a double (written as "{value}d").
					string raw = s.TrimEnd('d');
					if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
					{
						return d;
					}
				}

				// String is a Vector3 (written via ToParseableString()).
				if (s.TryParseVector3(out Vector3 vector3))
				{
					return vector3;
				}
			}

			// Regular string.
			return s;
		}

		private List<RuntimeDataEntry> FilterEntriesToWrite(RuntimeDataCollection collection)
		{
			List<RuntimeDataEntry> entriesToWrite = new List<RuntimeDataEntry>();

			if (collection == null || collection.Data == null)
			{
				return entriesToWrite;
			}

			foreach (RuntimeDataEntry entry in collection.Data)
			{
				if (entry == null ||
					(optimized && !entry.Dirty) ||
					entry.Value == null ||
					IsEmptyEnumerable(entry.Value) ||
					(entry is RuntimeDataCollection child && IsEffectivelyEmpty(child)))
				{
					continue;
				}

				entriesToWrite.Add(entry);
			}

			return entriesToWrite;
		}

		private bool IsEffectivelyEmpty(RuntimeDataCollection collection)
		{
			if (collection == null || collection.Data == null)
			{
				return true;
			}

			foreach (RuntimeDataEntry entry in collection.Data)
			{
				if (entry == null ||
					(optimized && !entry.Dirty) ||
					entry.Value == null ||
					IsEmptyEnumerable(entry.Value) ||
					(entry is RuntimeDataCollection child && IsEffectivelyEmpty(child)))
				{
					continue;
				}

				return false;
			}

			return true;
		}

		private static bool IsEmptyEnumerable(object value)
		{
			if (value == null)
			{
				return true;
			}

			// string is IEnumerable<char> but must never be treated like a list.
			if (value is string)
			{
				return false;
			}

			// Fast-path: ICollection has Count.
			if (value is ICollection col)
			{
				return col.Count == 0;
			}

			// Generic fallback: if it's IEnumerable but not ICollection, check whether it has at least one element.
			if (value is IEnumerable enumerable)
			{
				IEnumerator e = enumerable.GetEnumerator();
				try
				{
					return !e.MoveNext();
				}
				finally
				{
					(e as IDisposable)?.Dispose();
				}
			}

			return false;
		}
	}
}
