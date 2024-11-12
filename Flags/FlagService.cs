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

		public event Action<string, FlagData> SetFlagEvent;

		public IReadOnlyDictionary<string, FlagData> Flags => flags;

		private Dictionary<string, FlagData> flags;

		private TimeService timeService;
		private RuntimeDataService runtimeDataService;

		public FlagService(TimeService timeService, RuntimeDataService runtimeDataService)
		{
			this.timeService = timeService;
			this.runtimeDataService = runtimeDataService;

			runtimeDataService.CurrentProfileChangedEvent += OnProfileChangedEvent;
			runtimeDataService.SavingCurrentToDisk += OnSavingCurrentToDisk;

			Load();
		}

		/// <summary>
		/// (re)Load all saved flags from the currently loaded data profile.
		/// <see cref="RuntimeDataService"/>.
		/// </summary>
		public void Load()
		{
			if (runtimeDataService.CurrentProfile.TryGetEntry(DATA_FLAGS, out RuntimeDataCollection data))
			{
				flags = new Dictionary<string, FlagData>();
				foreach (RuntimeDataCollection entry in data.Data)
				{
					flags.Add(entry.ID, FlagData.FromRuntimeDataCollection(entry));
				}
			}
			else
			{
				flags = new Dictionary<string, FlagData>();
			}
		}

		/// <summary>
		/// Save all stored flags to the currently loaded data profile.
		/// <see cref="RuntimeDataService"/>.
		/// </summary>
		public void Save()
		{
			RuntimeDataCollection data = new RuntimeDataCollection(DATA_FLAGS);
			foreach (KeyValuePair<string, FlagData> flag in flags)
			{
				data.TryAdd(flag.Value.ToRuntimeDataCollection(flag.Key));
			}
			runtimeDataService.SaveDataToProfile(data);
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
			if (flags.ContainsKey(flag) && !overwrite)
			{
				return;
			}

			flags[flag] = flagData;
			SpaxDebug.Log("SetFlag:", $"{flag}\n{SpaxJsonUtils.Serialize(flagData)}");
			SetFlagEvent?.Invoke(flag, flagData);
		}

		/// <summary>
		/// Sets flag with id <paramref name="flag"/>.
		/// Creates a new <see cref="FlagData"/> object using the optional parameters.
		/// </summary>
		public void SetFlag(string flag, string setterID = "", TimeType timeType = TimeType.ScaledPlaytime, float expiration = 0f, bool overwrite = false)
		{
			SetFlag(flag, new FlagData(setterID, timeService.Time(timeType), timeType, expiration), overwrite);
		}

		/// <summary>
		/// Sets all of the flags in params <paramref name="flags"/> with default settings.
		/// </summary>
		/// <param name="flags">The flags to set.</param>
		public void SetFlags(string setterID, params string[] flags)
		{
			foreach (string flag in flags)
			{
				SetFlag(flag, setterID);
			}
		}

		/// <summary>
		/// Sets all of the flags in params <paramref name="flags"/> with default settings.
		/// </summary>
		/// <param name="flags">The flags to set.</param>
		public void SetFlags(string setterID, IEnumerable<string> flags)
		{
			foreach (string flag in flags)
			{
				SetFlag(flag, setterID);
			}
		}

		private void OnProfileChangedEvent(RuntimeDataCollection profile)
		{
			Load();
		}

		private void OnSavingCurrentToDisk(RuntimeDataCollection profile)
		{
			Save();
		}
	}
}
