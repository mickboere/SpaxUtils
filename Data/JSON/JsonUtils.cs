using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Utility class for writing JSON to file.
	/// </summary>
	/// https://code-maze.com/csharp-write-json-into-a-file/
	public static class JsonUtils
	{
		private static readonly JsonSerializerSettings OPTIONS = new()
		{
#if UNITY_EDITOR
			Formatting = Formatting.Indented,
#endif
			NullValueHandling = NullValueHandling.Ignore
		};

		public static void StreamWrite(object obj, string path)
		{
			using var streamWriter = File.CreateText(path);
			using var jsonWriter = new JsonTextWriter(streamWriter);
			JsonSerializer.CreateDefault(OPTIONS).Serialize(jsonWriter, obj);
		}

		public static T StreamRead<T>(string path)
		{
			if (!File.Exists(path))
			{
				return default(T);
			}

			using var streamReader = new StreamReader(path);
			string json = streamReader.ReadToEnd();
			SpaxDebug.Log($"JSON read result:", json);
			return JsonConvert.DeserializeObject<T>(json);
		}

		public static string Serialize(object data)
		{
			return JsonConvert.SerializeObject(data);
		}
	}
}
