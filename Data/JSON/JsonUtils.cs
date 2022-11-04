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
		private static readonly JsonSerializerSettings _options = new() { NullValueHandling = NullValueHandling.Ignore };

		public static void StreamWrite(object obj, string path)
		{
			using var streamWriter = File.CreateText(path);
			using var jsonWriter = new JsonTextWriter(streamWriter);
			JsonSerializer.CreateDefault(_options).Serialize(jsonWriter, obj);
		}

		public static T StreamRead<T>(string path)
		{
			if (!File.Exists(path))
			{
				return default(T);
			}

			using var streamReader = new StreamReader(path);
			string json = streamReader.ReadToEnd();
			return JsonConvert.DeserializeObject<T>(json);
		}
	}
}
