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
			runtimeDataService.SavingCurrentToDiskEvent += OnSavingCurrentToDisk;

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
			runtimeDataService.WriteToProfile(data);
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
		/// Returns whether a flag for id <paramref name="flag"/> exists.
		/// </summary>
		public bool TryGetFlag(string flag, out FlagData flagData)
		{
			if (flags.ContainsKey(flag))
			{
				flagData = flags[flag];
				return true;
			}

			flagData = null;
			return false;
		}

		/// <summary>
		/// Returns whether all the flags exist and have been completed.
		/// </summary>
		/// <returns>Whether all the flags currently exist and have been completed.</returns>
		public bool HasCompletedFlags(params string[] flags)
		{
			return flags.All((f) => this.flags.ContainsKey(f) && this.flags[f].Completed);
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
		public void SetFlag(string flag, string setterID = "", TimeType timeType = TimeType.ScaledPlaytime, float expiration = 0f, bool completed = false, bool overwrite = false)
		{
			SetFlag(flag, new FlagData(setterID, timeService.Time(timeType), timeType, expiration, completed), overwrite);
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
		public void SetFlags(IEnumerable<string> flags, string setterID = "", bool complete = false, bool overwrite = false)
		{
			foreach (string flag in flags)
			{
				SetFlag(flag, setterID, completed: complete, overwrite: overwrite);
			}
		}

		/// <summary>
		/// Sets all <paramref name="flags"/> to be completed.
		/// </summary>
		public void CompleteFlags(params string[] flags)
		{
			SetFlags(flags);
			foreach (string flag in flags)
			{
				if (TryGetFlag(flag, out FlagData data))
				{
					data.Completed = true;
				}
			}
		}

		/// <summary>
		/// Sets all <paramref name="flags"/> to be completed.
		/// </summary>
		public void CompleteFlags(IEnumerable<string> flags)
		{
			SetFlags(flags);
			foreach (string flag in flags)
			{
				if (TryGetFlag(flag, out FlagData data))
				{
					data.Completed = true;
				}
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
