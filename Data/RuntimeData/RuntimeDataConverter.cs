using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace SpaxUtils
{
	public class RuntimeDataConverter : JsonConverter<RuntimeDataEntry>
	{
		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, RuntimeDataEntry value, JsonSerializer serializer)
		{
			if (value is RuntimeDataCollection collection)
			{
				// Write RuntimeDataCollection.
				writer.WriteStartObject();
				writer.WritePropertyName(collection.ID);
				writer.WriteStartArray();
				foreach (RuntimeDataEntry item in collection.Data)
				{
					serializer.Serialize(writer, item, typeof(RuntimeDataEntry));
				}
				writer.WriteEndArray();
				writer.WriteEndObject();
			}
			else
			{
				// Write single RuntimeDataEntry (on single line).
				switch (value.Value)
				{
					case float f:
						writer.WriteRawValue($"{{\"{value.ID}\":\"{f}f\"}}");
						break;
					case double d:
						writer.WriteRawValue($"{{\"{value.ID}\":\"{d}d\"}}");
						break;
					case Vector3 v3:
						writer.WriteRawValue($"{{\"{value.ID}\":\"{v3.ToParseableString()}\"}}");
						break;
					default:
						JObject json = new JObject(new JProperty(value.ID, value.Value));
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
			if (token is JValue)
			{
				SpaxDebug.Error("JValue cannot be converted to RuntimeDataEntry:", token.ToString());
				return null;
			}

			RuntimeDataEntry result;
			string id = ((JProperty)token.First).Name;

			JToken data = token[id];
			switch (data.Type)
			{
				case JTokenType.Array:
					result = FromArray(id, data as JArray);
					break;
				case JTokenType.Object:
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
				default:
					SpaxDebug.Error("Could not process entry data:", $"tokenType={token.Type}, id={id}, dataTokenType={data.Type}");
					result = new RuntimeDataEntry(id, data);
					break;
			}
			return result;
		}

		private RuntimeDataEntry FromArray(string id, JArray array)
		{
			switch (array.First.Type)
			{
				case JTokenType.Object:
					// RuntimeDataCollection
					List<RuntimeDataEntry> entries = new List<RuntimeDataEntry>();
					foreach (JToken child in array)
					{
						entries.Add(GetEntry(child));
					}
					return new RuntimeDataCollection(id, entries);
				case JTokenType.String:
					// List<string>
					return new RuntimeDataEntry(id, array.ToObject<List<string>>());
				default:
					SpaxDebug.Error($"({id}) Unsuported array type: ({array.First.Type})", array.ToString());
					return null;
			}
		}

		private object GetValue(JToken token)
		{
			object result = token.ToObject<object>();
			SpaxDebug.Log("ToObject", $"token={token}\nresult={result}");
			return result;
		}

		private object GetStringValue(JToken token)
		{
			string s = (string)token;
			if (s.Length > 1 && (char.IsDigit(s[0]) || s[0] == '-'))
			{
				if (s.Last() == 'f' && float.TryParse(s.TrimEnd('f'), out float f))
				{
					// String is a float.
					return f;
				}
				if (s.Last() == 'd' && double.TryParse(s.TrimEnd('d'), out double d))
				{
					// String is a double.
					return d;
				}
				if (s.TryParseVector3(out Vector3 vector3))
				{
					// String is a Vector3.
					return vector3;
				}
			}

			// Regular string.
			return s;
		}
	}
}
