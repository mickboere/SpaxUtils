using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public class GlobalDataIdentifiers : ILabeledDataIdentifiers
	{
		private const string GLOBAL = "GLOBAL/";

		// Profile caching
		public const string PROFILES = GLOBAL + "Profiles/";
		// Timekeeping
		public const string LAST_SAVE = PROFILES + "LastSave";
		public const string INITIAL_TIME = PROFILES + "InitialTime";
		public const string PLAYTIME_UNSCALED = PROFILES + "UnscaledPlaytime";
		public const string PLAYTIME_SCALED = PROFILES + "ScaledPlaytime";
		

		//private const string SETTINGS = "SETTINGS/";
	}
}
