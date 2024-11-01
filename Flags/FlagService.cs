using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// Global service that keeps track of all set game flags.
	/// </summary>
	public class FlagService : IService
	{
		private const string DATA_FLAGS = "FLAGS";

		public Action<string, FlagData> SetFlagEvent;

		public IReadOnlyDictionary<string, FlagData> Flags => flags;

		private Dictionary<string, FlagData> flags;
		private RuntimeDataCollection data;

		private TimeService timeService;
		private RuntimeDataService runtimeDataService;

		public FlagService(TimeService timeService, RuntimeDataService runtimeDataService)
		{
			this.timeService = timeService;
			this.runtimeDataService = runtimeDataService;
			Load();
			Save();
		}

		/// <summary>
		/// (re)Load all saved flags from the currently loaded data profile.
		/// <see cref="RuntimeDataService"/>.
		/// </summary>
		public void Load()
		{
			if (runtimeDataService.CurrentProfile.TryGetEntry(DATA_FLAGS, out data))
			{
				flags = new Dictionary<string, FlagData>();
				foreach (RuntimeDataEntry entry in data.Data)
				{
					flags.Add(entry.ID, (FlagData)entry.Value);
				}
			}
			else
			{
				flags = new Dictionary<string, FlagData>();
				data = new RuntimeDataCollection(DATA_FLAGS);
			}
		}

		/// <summary>
		/// Save all stored flags to the currently loaded data profile.
		/// <see cref="RuntimeDataService"/>.
		/// </summary>
		public void Save()
		{
			runtimeDataService.CurrentProfile.TryAdd(data, true);
		}

		/// <summary>
		/// Returns whether all the flags currently exist.
		/// </summary>
		/// <returns>Whether all the flags currently exist.</returns>
		public bool HasFlags(params string[] flags)
		{
			return flags.All((f) => this.flags.ContainsKey(f));
		}

		/// <summary>
		/// Sets flag with id <paramref name="flag"/>.
		/// </summary>
		public void SetFlag(string flag, FlagData flagData, bool overwrite = false)
		{
			if (Flags.ContainsKey(flag) && !overwrite)
			{
				return;
			}

			flags[flag] = flagData;
			data[flag] = new RuntimeDataEntry(flag, flagData);
			SpaxDebug.Log("SetFlag:", $"{flag}\n{SpaxJsonUtils.Serialize(flagData)}");
			SetFlagEvent?.Invoke(flag, flagData);
		}

		/// <summary>
		/// Sets flag with id <paramref name="flag"/>.
		/// Creates a new <see cref="FlagData"/> object using the optional parameters.
		/// </summary>
		public void SetFlag(string flag, string owner = "", TimeType timeType = TimeType.ScaledPlaytime, float expiration = -1f, bool overwrite = false)
		{
			SetFlag(flag, new FlagData(owner, timeType, timeService.Time(timeType), expiration), overwrite);
		}

		/// <summary>
		/// Sets all of the flags in params <paramref name="flags"/> with default settings.
		/// </summary>
		/// <param name="flags">The flags to set.</param>
		public void SetFlags(params string[] flags)
		{
			foreach (string flag in flags)
			{
				SetFlag(flag);
			}
		}

		/// <summary>
		/// Sets all of the flags in params <paramref name="flags"/> with default settings.
		/// </summary>
		/// <param name="flags">The flags to set.</param>
		public void SetFlags(IEnumerable<string> flags)
		{
			foreach (string flag in flags)
			{
				SetFlag(flag);
			}
		}
	}
}
