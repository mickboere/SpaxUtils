using System;
using System.Collections.Generic;

namespace SpaxUtils
{
	/// <summary>
	/// Class containing flag data with easy conversion methods to and from <see cref="RuntimeDataCollection"/>.
	/// </summary>
	[Serializable]
	public class FlagData
	{
		/// <summary>
		/// The ID of the entity that set this flag.
		/// </summary>
		public string Setter { get; }

		/// <summary>
		/// The time this flag was set.
		/// </summary>
		public float Time { get; }

		/// <summary>
		/// The type of time used for determining when this flag was set.
		/// </summary>
		public TimeType TimeType { get; }

		/// <summary>
		/// Total expiration time in seconds.
		/// <= 0 = never expires.
		/// </summary>
		public float Expiration { get; }

		/// <summary>
		/// Whether the flag's set objective has been completed.
		/// </summary>
		public bool Completed { get; set; }

		public FlagData(string setter, float time, TimeType timeType = TimeType.ScaledPlaytime, float expiration = 0f, bool completed = false)
		{
			Setter = setter;
			TimeType = timeType;
			Time = time;
			Expiration = expiration;
			Completed = completed;
		}

		public RuntimeDataCollection ToRuntimeDataCollection(string id)
		{
			// Only store non-default data.

			List<RuntimeDataEntry> data = new List<RuntimeDataEntry>();
			if (!string.IsNullOrEmpty(Setter))
			{
				data.Add(new RuntimeDataEntry(nameof(Setter), Setter, true));
			}
			data.Add(new RuntimeDataEntry(nameof(Time), Time, true));
			if (TimeType != TimeType.ScaledPlaytime)
			{
				data.Add(new RuntimeDataEntry(nameof(TimeType), TimeType, true));
			}
			if (Expiration > 0)
			{
				data.Add(new RuntimeDataEntry(nameof(Expiration), Expiration, true));
			}
			if (Completed)
			{
				data.Add(new RuntimeDataEntry(nameof(Completed), Completed, true));
			}
			return new RuntimeDataCollection(id, data);
		}

		public static FlagData FromRuntimeDataCollection(RuntimeDataCollection data)
		{
			return new FlagData(
				data.GetValue(nameof(Setter), ""),
				data.GetValue(nameof(Time), 0f),
				(TimeType)data.GetValue(nameof(TimeType), 0),
				data.GetValue(nameof(Expiration), 0f),
				data.GetValue(nameof(Completed), false));
		}
	}
}
