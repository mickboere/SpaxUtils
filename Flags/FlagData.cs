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

		public FlagData(string owner, float time, TimeType timeType = TimeType.ScaledPlaytime, float expiration = 0f)
		{
			Setter = owner;
			TimeType = timeType;
			Time = time;
			Expiration = expiration;
		}

		public RuntimeDataCollection ToRuntimeDataCollection(string id)
		{
			List<RuntimeDataEntry> data = new List<RuntimeDataEntry>();
			data.Add(new RuntimeDataEntry(nameof(Setter), Setter));
			data.Add(new RuntimeDataEntry(nameof(Time), Time));
			if (TimeType != TimeType.ScaledPlaytime)
			{
				// No need to store time type if it is the default type.
				data.Add(new RuntimeDataEntry(nameof(TimeType), TimeType));
			}
			if (Expiration > 0)
			{
				// No need to store expiration if there is no expiration.
				data.Add(new RuntimeDataEntry(nameof(Expiration), Expiration));
			}
			return new RuntimeDataCollection(id, data);
		}

		public static FlagData FromRuntimeDataCollection(RuntimeDataCollection data)
		{
			return new FlagData(
				data.GetValue(nameof(Setter), ""),
				data.GetValue(nameof(Time), 0f),
				(TimeType)data.GetValue(nameof(TimeType), 0),
				data.GetValue(nameof(Expiration), 0f));
		}
	}
}