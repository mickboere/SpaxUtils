using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Service responsible for loading savedata from the disk and converting it into runtime data.
	/// </summary>
	public class RuntimeDataService : IService
	{
		public const string GLOBAL_DATA_ID = "GLOBAL";
		public const string PROFILE_FILE_TYPE = ".save";
		public static readonly string PROFILES_PATH = $"{Application.persistentDataPath}/Profiles/";

		/// <summary>
		/// Invoked once <see cref="CurrentProfile"/> has changed.
		/// </summary>
		public event Action<RuntimeDataCollection> CurrentProfileChangedEvent;

		/// <summary>
		/// The global data profile used for storing settings across all profiles.
		/// </summary>
		public RuntimeDataCollection GlobalData { get; private set; }

		/// <summary>
		/// The current profile to which all undirected data is saved.
		/// </summary>
		public RuntimeDataCollection CurrentProfile
		{
			get { return _currentProfile; }
			private set
			{
				_currentProfile = value;
				CurrentProfileChangedEvent?.Invoke(value);
				SpaxDebug.Log("Switched current profile:", CurrentProfile == null ? "NULL" : CurrentProfile.ID);
			}
		}
		private RuntimeDataCollection _currentProfile;

		/// <summary>
		/// All available profiles.
		/// Not all profiles are guaranteed to have data as the data is loaded on demand.
		/// </summary>
		public Dictionary<string, RuntimeDataCollection> Profiles { get; private set; }

		/// <summary>
		/// Data collection stored under <see cref="GlobalDataIdentifiers.PROFILES"/> within the <see cref="GlobalData"/>.
		/// Contains summarized data for all cached profiles, so that the entire profile need not be loaded to retrieve surface-level data.
		/// Each entry within this collection is in itself a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection ProfilesMetaData => _profilesMetaData;
		private RuntimeDataCollection _profilesMetaData;

		public RuntimeDataService()
		{
			// Ensure data directory.
			if (!Directory.Exists(PROFILES_PATH))
			{
				Directory.CreateDirectory(PROFILES_PATH);
			}

			// Collect profiles, but don't load them.
			Profiles = Directory.GetFiles(PROFILES_PATH).ToDictionary<string, string, RuntimeDataCollection>(k => Path.GetFileNameWithoutExtension(k), v => null);
			if (Profiles.ContainsKey(GLOBAL_DATA_ID)) Profiles.Remove(GLOBAL_DATA_ID);

			// Load/Create Global data profile.
			if (LoadProfile(GLOBAL_DATA_ID, out RuntimeDataCollection globalData, false, true))
			{
				GlobalData = globalData;

				// Initialize profile meta data.
				if (!globalData.TryGetEntry(GlobalDataIdentifiers.PROFILES, out _profilesMetaData))
				{
					// Cache does not exist yet, generate it.
					_profilesMetaData = new RuntimeDataCollection(GlobalDataIdentifiers.PROFILES);
					if (Profiles.Count > 0)
					{
						foreach (RuntimeDataCollection profile in Profiles.Values)
						{
							_profilesMetaData.TryAdd(new RuntimeDataCollection(profile.ID));
						}
					}
					globalData.TryAdd(_profilesMetaData);
				}
			}
			else
			{
				SpaxDebug.Error("Global data could not be loaded!");
			}
		}

		/// <summary>
		/// Creates a new profile / root <see cref="RuntimeDataCollection"/>.
		/// </summary>
		/// <param name="profileId">The unique ID to name this profile.</param>
		/// <param name="setAsCurrent">Once created, should the new profile be set as the <see cref="CurrentProfile"/>?</param>
		/// <param name="saveToDisk">Once created, should the new profile immediately be saved to disk? <seealso cref="SaveProfileToDisk(string)"/></param>
		/// <param name="profile">The resulting profile <see cref="RuntimeDataCollection"/>.</param>
		/// <returns>Whether the profile was created successfully.</returns>
		public bool CreateProfile(string profileId, out RuntimeDataCollection profile, bool setAsCurrent = false, bool saveToDisk = false)
		{
			profile = null;

			// Prevent duplicate profiles.
			if (Profiles.ContainsKey(profileId))
			{
				SpaxDebug.Error("Couldn't create profile.", $"Profile with ID \"{profileId}\" already exists.");
				return false;
			}

			// Create new profile.
			profile = new RuntimeDataCollection(profileId);

			// If profile isn't global, store it in profile collection.
			if (profileId != GLOBAL_DATA_ID)
			{
				Profiles.Add(profileId, profile);

				var metaData = new RuntimeDataCollection(profileId);
				ProfilesMetaData.TryAdd(metaData);

				if (setAsCurrent)
				{
					SetCurrentProfile(profile);
				}
			}

			if (saveToDisk)
			{
				SaveProfileToDisk(profileId);
			}

			return true;
		}

		#region Loading

		/// <summary>
		/// Attempt to load a profile with ID <paramref name="profileId"/>.
		/// </summary>
		/// <param name="profileId">The ID of the profile to attempt to load.</param>
		/// <param name="setAsCurrent">Once loaded, should the profile be set as <see cref="CurrentProfile"/>?</param>
		/// <param name="data">The resulting loaded <see cref="RuntimeDataCollection"/>.</param>
		/// <returns>Whether loading the profile was a success.</returns>
		public bool LoadProfile(string profileId, out RuntimeDataCollection data, bool setAsCurrent = false, bool createIfNull = false)
		{
			// Check if global data.
			if (profileId == GLOBAL_DATA_ID && GlobalData != null)
			{
				// Global data is already loaded, return it.
				data = GlobalData;
				return true;
			}

			// Check if already loaded.
			if (Profiles.ContainsKey(profileId) && Profiles[profileId] != null)
			{
				if (setAsCurrent)
				{
					SetCurrentProfile(Profiles[profileId]);
				}

				data = Profiles[profileId];
				return true;
			}

			// Load from disk.
			data = SpaxJsonUtils.StreamRead<RuntimeDataCollection>(PROFILES_PATH + profileId + PROFILE_FILE_TYPE);
			if (data != null)
			{
				if (profileId != GLOBAL_DATA_ID)
				{
					Profiles[profileId] = data;
				}
				SpaxDebug.Log($"Loaded profile from \"{PROFILES_PATH + profileId + PROFILE_FILE_TYPE}\":\n", data.ToString());
			}
			else if (createIfNull)
			{
				// No data exists yet, create it.
				CreateProfile(profileId, out data, false, false);
			}

			if (setAsCurrent)
			{
				SetCurrentProfile(data);
			}

			return data != null;
		}

		/// <summary>
		/// Unloads profile with ID <paramref name="profileId"/> if it is loaded.
		/// If it is the currently active profile, <see cref="CurrentProfile"/> will be set to null.
		/// </summary>
		public void UnloadProfile(string profileId, bool fireEvent = true)
		{
			bool wasCurrent = false;
			if (CurrentProfile != null && CurrentProfile.ID == profileId)
			{
				wasCurrent = true;
			}

			if (Profiles.ContainsKey(profileId) && Profiles[profileId] != null)
			{
				Profiles[profileId].Dispose();
				Profiles[profileId] = null;
			}

			if (wasCurrent)
			{
				if (fireEvent)
				{
					CurrentProfile = null;
				}
				else
				{
					_currentProfile = null;
				}
			}

			SpaxDebug.Log("Unloaded profile:", profileId);
		}

		/// <summary>
		/// Returns the profile with the most recent save time, if any.
		/// </summary>
		public bool TryGetLastSavedMetaData(out RuntimeDataCollection result, bool loadResultAsCurrent = false)
		{
			result = null;
			if (ProfilesMetaData.Data.Count == 0)
			{
				// No meta data available.
				return false;
			}

			DateTime lastSave = new DateTime(0);
			foreach (RuntimeDataCollection metaData in ProfilesMetaData.Data)
			{
				DateTime saveTime;

				RuntimeDataEntry saveTimeEntry = metaData.GetEntry(GlobalDataIdentifiers.LAST_SAVE);
				if (saveTimeEntry == null)
				{
					SpaxDebug.Error("Profile does not contain last save time.\n", metaData.ToString());
					if (result != null) continue;
				}

				if (!DateTime.TryParse((string)saveTimeEntry.Value, out saveTime))
				{
					SpaxDebug.Error($"Could not parse last save time: {saveTimeEntry.Value}", metaData.ToString());
					if (result != null) continue;
				}

				if (result == null || saveTime > lastSave)
				{
					result = metaData;
					lastSave = saveTime;
				}
			}

			if (loadResultAsCurrent)
			{
				LoadProfile(result.ID, out _, true);
			}

			return true;
		}

		/// <summary>
		/// Retrieve meta data for profile with ID <paramref name="profileId"/>, <see cref="CurrentProfile"/> if null.
		/// </summary>
		public RuntimeDataCollection GetMetaData(string profileId = null)
		{
			if (profileId == null)
			{
				if (CurrentProfile != null)
				{
					profileId = CurrentProfile.ID;
				}
				else
				{
					SpaxDebug.Error("No meta data to return.", "No profile ID was provided and there is no currently selected profile.");
					return null;
				}
			}

			return ProfilesMetaData.GetEntry<RuntimeDataCollection>(profileId);
		}

		#endregion Loading

		#region Saving

		/// <summary>
		/// Saves the given <paramref name="data"/> entry to the root of the specified profile, <see cref="CurrentProfile"/> if null.
		/// </summary>
		public void SaveDataToProfile(RuntimeDataEntry data, string profileId = null, bool overwrite = true)
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

		/// <summary>
		/// Attempt to save a loaded profile's contents to the disk.
		/// </summary>
		/// <param name="profileId">The ID of the profile to save to the disk.</param>
		/// <returns>Whether the saving to disk was a success.</returns>
		public bool SaveProfileToDisk(string profileId = null)
		{
			if (profileId == null)
			{
				if (CurrentProfile != null)
				{
					profileId = CurrentProfile.ID;
				}
				else
				{
					SpaxDebug.Error("Cannot save data to disk.", "No profile ID was provided and there is no currently selected profile.");
					return false;
				}
			}

			// Retrieve the profile that will be saved to the disk.
			RuntimeDataCollection profileData = null;
			if (profileId != GLOBAL_DATA_ID)
			{
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

				profileData = Profiles[profileId];

				// Save profile meta data to global data.
				RuntimeDataCollection metaData = ProfilesMetaData.GetEntry<RuntimeDataCollection>(profileId);
				metaData.SetValue(GlobalDataIdentifiers.LAST_SAVE, DateTime.UtcNow.ToString());
				SaveProfileToDisk(GLOBAL_DATA_ID);
			}
			else
			{
				profileData = GlobalData;
			}

			// Save data to disk.
			//SpaxDebug.Notify($"Saving data for profile: {profileId}\n{profileData}");
			SpaxJsonUtils.StreamWrite(profileData, PROFILES_PATH + profileData.ID + PROFILE_FILE_TYPE);
			return true;
		}

		#endregion Saving

		private void SetCurrentProfile(RuntimeDataCollection profile, bool unloadPrevious = true)
		{
			if (profile == CurrentProfile)
			{
				return; // Already set.
			}
			if (unloadPrevious && CurrentProfile != null)
			{
				UnloadProfile(CurrentProfile.ID, false); // Unload currently selected profile but don't fire event.
			}

			// Set the profile and allow event to be fired.
			CurrentProfile = profile;
		}
	}
}
