﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service responsible for loading savedata from the disk and converting it into runtime data.
	/// </summary>
	public class RuntimeDataService : IService, IDisposable
	{
		public const string GLOBAL_PROFILE = "GLOBAL";
		public const string PROFILE_FILE_TYPE = ".save";
		public static readonly string PROFILES_PATH = $"{Application.persistentDataPath}/Profiles/";

		/// <summary>
		/// The current profile to which all undirected data is saved.
		/// </summary>
		public RuntimeDataCollection CurrentProfile { get; private set; }

		/// <summary>
		/// All loaded profiles.
		/// </summary>
		public Dictionary<string, RuntimeDataCollection> Profiles { get; private set; }

		public RuntimeDataService()
		{
			if (!Directory.Exists(PROFILES_PATH))
			{
				Directory.CreateDirectory(PROFILES_PATH);
			}

			// Collect profiles but don't load them.
			Profiles = Directory.GetFiles(PROFILES_PATH).ToDictionary<string, string, RuntimeDataCollection>(k => k, v => null);

			// Switch to global profile by default.
			LoadProfile(GLOBAL_PROFILE, true, out _, true);
		}

		public void Dispose()
		{
			if (Profiles != null)
			{
				foreach (KeyValuePair<string, RuntimeDataCollection> profile in Profiles)
				{
					profile.Value?.Dispose();
				}
			}
		}

		/// <summary>
		/// Creates a new profile / root <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="profileId">The unique ID to name this profile.</param>
		/// <param name="setAsCurrent">Once created, should the new profile be set as the <see cref="CurrentProfile"/>?</param>
		/// <param name="saveToDisk">Once created, should the new profile immediately be saved to disk? <seealso cref="SaveToDisk(string)"/></param>
		/// <param name="profile">The resulting profile <see cref="RuntimeDataCollection"/>.</param>
		/// <returns>Whether the profile was created successfully.</returns>
		public bool CreateProfile(string profileId, bool setAsCurrent, bool saveToDisk, out RuntimeDataCollection profile)
		{
			profile = null;

			if (Profiles.ContainsKey(profileId))
			{
				SpaxDebug.Error("Couldn't create profile.", $"Profile with ID <color=red>\"{profileId}\"</color=red> already exists.");
				return false;
			}

			profile = new RuntimeDataCollection(profileId);
			Profiles.Add(profileId, profile);

			if (setAsCurrent)
			{
				CurrentProfile = profile;
			}

			if (saveToDisk)
			{
				SaveToDisk(profileId);
			}

			return true;
		}

		/// <summary>
		/// Attempt to save a loaded profile's contents to the disk.
		/// </summary>
		/// <param name="profileId">The ID of the profile to save to the disk.</param>
		/// <returns>Whether the saving to disk was a success.</returns>
		public bool SaveToDisk(string profileId = null)
		{
			if (profileId == null)
			{
				profileId = CurrentProfile.ID;
			}

			if (!Profiles.ContainsKey(profileId))
			{
				SpaxDebug.Error("Couldn't save profile.", $"No profile loaded with ID <color=red>\"{profileId}\"</color=red>.");
				return false;
			}
			else if (Profiles[profileId] == null)
			{
				SpaxDebug.Error("Couldn't save profile.", $"Profile with ID <color=red>\"{profileId}\"</color=red> is not loaded yet.");
				return false;
			}

			// Save to disk.
			RuntimeDataCollection data = Profiles[profileId];
			SpaxDebug.Log($"Saving data for profile: {profileId}\n{data}");
			JsonUtils.StreamWrite(data, PROFILES_PATH + data.ID + PROFILE_FILE_TYPE);
			return true;
		}

		/// <summary>
		/// Attempt to load a profile with ID <paramref name="profileId"/>.
		/// </summary>
		/// <param name="profileId">The ID of the profile to attempt to load.</param>
		/// <param name="setAsCurrent">Once loaded, should the profile be set as <see cref="CurrentProfile"/>?</param>
		/// <param name="data">The resulting loaded <see cref="RuntimeDataCollection"/>.</param>
		/// <returns>Whether loading the profile was a success.</returns>
		public bool LoadProfile(string profileId, bool setAsCurrent, out RuntimeDataCollection data, bool createIfNull = false)
		{
			// Check if already loaded.
			if (Profiles.ContainsKey(profileId) && Profiles[profileId] != null)
			{
				if (setAsCurrent)
				{
					CurrentProfile = Profiles[profileId];
				}

				data = Profiles[profileId];
				return true;
			}

			// Load from disk.
			data = JsonUtils.StreamRead<RuntimeDataCollection>(PROFILES_PATH + profileId + PROFILE_FILE_TYPE);

			if (data == null && createIfNull)
			{
				CreateProfile(profileId, false, false, out data);
			}

			if (setAsCurrent)
			{
				CurrentProfile = data;
			}

			Profiles[profileId] = data;
			return data != null;
		}

		/// <summary>
		/// Saves the given <paramref name="data"/> entry to the root of the specified profile, <see cref="CurrentProfile"/> if null.
		/// </summary>
		public void Save(RuntimeDataEntry data, string profileId = null, bool overwrite = true)
		{
			if (profileId == null)
			{
				CurrentProfile.TryAdd(data, overwrite);
				return;
			}

			if (!Profiles.ContainsKey(profileId))
			{
				SpaxDebug.Error("Couldn't save data.", $"No profile loaded with ID <color=red>\"{profileId}\"</color=red>.");
				return;
			}
			else if (Profiles[profileId] == null)
			{
				SpaxDebug.Error("Couldn't save data.", $"Profile with ID <color=red>\"{profileId}\"</color=red> is not loaded yet.");
				return;
			}

			RuntimeDataCollection profile = Profiles[profileId];
			profile.TryAdd(data, overwrite);
		}
	}
}