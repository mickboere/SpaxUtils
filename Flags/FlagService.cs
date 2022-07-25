using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaxUtils
{
	/// <summary>
	/// TODO: SAVING / LOADING
	/// </summary>
	public class FlagService : IService
	{
		public Action<string, FlagData> SetFlagEvent;

		public IReadOnlyDictionary<string, FlagData> Flags => flags;

		private Dictionary<string, FlagData> flags = new Dictionary<string, FlagData>();
		private TimeService timeService;

		public FlagService(TimeService timeService)
		{
			this.timeService = timeService;
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
		public void SetFlag(string flag, FlagData data, bool overwrite = false)
		{
			if (Flags.ContainsKey(flag) && !overwrite)
			{
				return;
			}

			flags[flag] = data;
			SetFlagEvent?.Invoke(flag, data);
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
	}
}
