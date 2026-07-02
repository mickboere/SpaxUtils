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
		public const string PROFILE_FILE_TYPE = ".save";
		public const string GLOBAL_FILE_TYPE = ".data";
		public static readonly string PROFILES_PATH = $"{Application.persistentDataPath}/Profiles/";
		public const string GLOBAL_DATA_ID = "GLOBAL";
		public const string DEFAULT_PROFILE_ID = "DEFAULT";

		/// <summary>
		/// ID of the metadata collection stored within each profile (name, last save time, playtime, ...).
		/// Kept inside the profile file itself so the file is self-describing and survives external copies/reverts.
		/// </summary>
		public const string META_DATA_ID = "PROFILE/Meta";

		/// <summary>
		/// Invoked once <see cref="CurrentProfile"/> has changed.
		/// </summary>
		public event Action<RuntimeDataCollection> CurrentProfileChangedEvent;

		/// <summary>
		/// Invoked just before a profile is saved to the disk.
		/// </summary>
		public event Action<RuntimeDataCollection> SavingToDiskEvent;

		/// <summary>
		/// Invoked just before a profile is saved to the disk.
		/// </summary>
		public event Action<RuntimeDataCollection> SavingCurrentToDiskEvent;

		/// <summary>
		/// Whether auto save is enabled or disabled.
		/// </summary>
		public bool AutoSave { get; }

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
		/// All available profiles, keyed by their unique (GUID) identity.
		/// Not all profiles are guaranteed to have data as the data is loaded on demand.
		/// </summary>
		public Dictionary<string, RuntimeDataCollection> Profiles { get; private set; }

		/// <summary>
		/// Derived, display-only index of all discovered profiles' metadata, keyed by profile ID.
		/// Rebuilt from the profiles' own on-disk metadata each launch, so it can never desync from the files.
		/// Each entry within this collection is in itself a <see cref="RuntimeDataCollection"/>.
		/// </summary>
		public RuntimeDataCollection ProfilesMetaData => _profilesMetaData;
		private RuntimeDataCollection _profilesMetaData;

		/// <summary>
		/// Maps each known profile ID (GUID) to the file it was discovered at / is saved to.
		/// The filename is human-readable ("{Name} [{id}]") but identity is always the GUID.
		/// </summary>
		private Dictionary<string, string> profileFilePaths;

		public RuntimeDataService()
		{
			// Ensure data directory.
			if (!Directory.Exists(PROFILES_PATH))
			{
				Directory.CreateDirectory(PROFILES_PATH);
			}

			// Migrate a legacy global data file to its dedicated extension, if present.
			MigrateLegacyGlobalFile();

			// Initialize collections up-front so LoadProfile can run during construction.
			Profiles = new Dictionary<string, RuntimeDataCollection>();
			profileFilePaths = new Dictionary<string, string>();
			_profilesMetaData = new RuntimeDataCollection(GlobalDataIdentifiers.PROFILES);

			// Load/Create Global data profile (holds cross-profile settings; not a player profile).
			if (LoadProfile(GLOBAL_DATA_ID, out RuntimeDataCollection globalData, false, true))
			{
				GlobalData = globalData;
			}
			else
			{
				SpaxDebug.Error("Global data could not be loaded!");
			}

			// Discover profiles from disk: resolve their GUID identity, migrate legacy (name-keyed) saves,
			// de-duplicate copies, and build the derived metadata index. Profile files are the source of truth.
			CollectProfiles();
		}

		#region Profiles

		/// <summary>
		/// Creates a new profile / root <see cref="RuntimeDataCollection"/> with a unique (GUID) identity.
		/// </summary>
		/// <param name="name">The display name for this profile (the player's chosen character name). Not the identity.</param>
		/// <param name="setAsCurrent">Once created, should the new profile be set as the <see cref="CurrentProfile"/>?</param>
		/// <param name="saveToDisk">Once created, should the new profile immediately be saved to disk? <seealso cref="SaveProfileToDisk(string)"/></param>
		/// <param name="profile">The resulting profile <see cref="RuntimeDataCollection"/>.</param>
		/// <returns>Whether the profile was created successfully.</returns>
		public bool CreateProfile(string name, out RuntimeDataCollection profile, bool setAsCurrent = false, bool saveToDisk = false)
		{
			profile = null;

			// The global data profile keeps its reserved literal ID; player profiles get a unique GUID
			// so their identity can never collide with a chosen character name.
			bool isGlobal = name == GLOBAL_DATA_ID;
			string id = isGlobal ? GLOBAL_DATA_ID : Guid.NewGuid().ToString();

			// Prevent duplicate profiles (GUID collisions are effectively impossible, but stay safe).
			if (Profiles.ContainsKey(id))
			{
				SpaxDebug.Error("Couldn't create profile.", $"Profile with ID \"{id}\" already exists.");
				return false;
			}

			// Create new profile.
			profile = new RuntimeDataCollection(id);

			// If profile isn't global, store it in the profile collection and set up its metadata.
			if (!isGlobal)
			{
				profile.SetValue(ProfileDataIdentifiers.SEED, name.GetDeterministicHashCode());

				RuntimeDataCollection meta = profile.GetEntry<RuntimeDataCollection>(META_DATA_ID, new RuntimeDataCollection(META_DATA_ID));
				meta.SetValue(ProfileDataIdentifiers.NAME, name);

				Profiles.Add(id, profile);
				profileFilePaths[id] = GetCanonicalProfilePath(name, id);
				IndexProfileMeta(id, meta);

				if (setAsCurrent)
				{
					SetCurrentProfile(profile);
				}
			}

			if (saveToDisk)
			{
				SaveProfileToDisk(id);
			}

			return true;
		}

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

		#endregion Profiles

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
			string path = GetProfilePath(profileId);
			data = path != null ? SpaxJsonUtils.StreamRead<RuntimeDataCollection>(path) : null;
			if (data != null)
			{
				if (profileId != GLOBAL_DATA_ID)
				{
					Profiles[profileId] = data;
				}
				SpaxDebug.Log($"Loaded profile from \"{path}\":\n", data.ToString());
			}
			else if (createIfNull)
			{
				// No data exists yet, create it. Note: for a non-global ID this treats it as a name
				// and mints a fresh GUID identity (used for the default fallback profile).
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
		/// Ensure a current profile is loaded.
		/// </summary>
		/// <param name="useLastSave">Whether to try and load the last saved profile if there is no current profile.</param>
		/// <param name="defaultIfNull">The default profile name to create if there is no available profile.</param>
		/// <returns>The current loaded profile.</returns>
		public RuntimeDataCollection EnsureCurrentProfile(bool useLastSave = true, string defaultIfNull = DEFAULT_PROFILE_ID)
		{
			if (CurrentProfile != null)
			{
				return CurrentProfile;
			}

			if (useLastSave && TryGetLastSavedMetaData(out RuntimeDataCollection meta, true))
			{
				// TryGetLastSavedMetaData(loadResultAsCurrent: true) loads and sets CurrentProfile.
				if (CurrentProfile != null && CurrentProfile.ID == meta.ID)
				{
					return CurrentProfile;
				}

				// Fallback: force-load and return it.
				if (LoadProfile(meta.ID, out RuntimeDataCollection loaded, true, true))
				{
					return loaded;
				}
			}

			// Nothing to load - create a fresh, default-named profile (gets its own GUID identity).
			if (LoadProfile(defaultIfNull, out RuntimeDataCollection data, true, true))
			{
				return data;
			}

			SpaxDebug.Error("Somehow, no save profile could be ensured.", $"defaultIfNull=\"{defaultIfNull}\"");
			return null;
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

			// If the profile is loaded, return its own (live) metadata collection so that writes
			// (e.g. playtime, last save) persist with the profile itself - this is the source of truth.
			RuntimeDataCollection profile = GetLoadedProfile(profileId);
			if (profile != null)
			{
				return profile.GetEntry<RuntimeDataCollection>(META_DATA_ID, new RuntimeDataCollection(META_DATA_ID));
			}

			// Otherwise fall back to the derived (display-only) index entry.
			return ProfilesMetaData.GetEntry<RuntimeDataCollection>(profileId);
		}

		/// <summary>
		/// Returns the display name of profile <paramref name="profileId"/> (<see cref="CurrentProfile"/> if null).
		/// This is the player's chosen character name, which is distinct from the profile's (GUID) identity.
		/// </summary>
		public string GetProfileName(string profileId = null)
		{
			RuntimeDataCollection meta = GetMetaData(profileId);
			string name = meta?.GetValue<string>(ProfileDataIdentifiers.NAME);
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
			return profileId ?? (CurrentProfile != null ? CurrentProfile.ID : null);
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
				RuntimeDataEntry saveTimeEntry = metaData.GetEntry(GlobalDataIdentifiers.LAST_SAVE);
				if (saveTimeEntry == null)
				{
					// Metadata without a save time is not a valid candidate (freshly discovered profile,
					// regenerated global data, or an externally-added file); skip it rather than crash.
					SpaxDebug.Error("Profile does not contain last save time.\n", metaData.ToString());
					continue;
				}

				if (!DateTime.TryParse(saveTimeEntry.Value as string, out DateTime saveTime))
				{
					SpaxDebug.Error($"Could not parse last save time: {saveTimeEntry.Value}", metaData.ToString());
					continue;
				}

				if (result == null || saveTime > lastSave)
				{
					result = metaData;
					lastSave = saveTime;
				}
			}

			if (result == null)
			{
				// No profile with a valid last save time was found.
				return false;
			}

			if (loadResultAsCurrent)
			{
				LoadProfile(result.ID, out _, true);
			}

			return true;
		}

		#endregion Loading

		#region Saving

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
			RuntimeDataCollection profileData;
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

				// Stamp the last-save time into the profile's own metadata (the source of truth), then
				// refresh the display index so the load screen stays current within this session.
				RuntimeDataCollection metaData = profileData.GetEntry<RuntimeDataCollection>(META_DATA_ID, new RuntimeDataCollection(META_DATA_ID));
				metaData.SetValue(GlobalDataIdentifiers.LAST_SAVE, DateTime.UtcNow.ToString());
				IndexProfileMeta(profileId, metaData);
			}
			else
			{
				profileData = GlobalData;
			}

			SavingToDiskEvent?.Invoke(profileData);
			if (CurrentProfile != null && profileId == CurrentProfile.ID)
			{
				SavingCurrentToDiskEvent?.Invoke(profileData);
			}

			// Save data to disk under its canonical "{Name} [{id}]" filename (or the global data file).
			string savePath = GetSaveTargetPath(profileData);
			SpaxDebug.Log($"Saving profile to disk: {profileId}\n{profileData.ToStringOptimized()}");
			SpaxJsonUtils.StreamWrite(profileData, savePath);
			if (profileData.ID != GLOBAL_DATA_ID)
			{
				profileFilePaths[profileData.ID] = savePath;
			}
			return true;
		}

		/// <summary>
		/// Create an automatic save to the disk, if <see cref="AutoSave"/> is enabled.
		/// </summary>
		public void AutoSaveToDisk()
		{
			if (AutoSave)
			{
				SaveProfileToDisk();
			}
		}

		#endregion Saving

		#region Writing

		/// <summary>
		/// Saves a CLONE of the given <paramref name="data"/> entry to the root of the specified profile, <see cref="CurrentProfile"/> if null.
		/// A clone is written to prevent data references from unintentionally altering profile data.
		/// </summary>
		public void WriteToProfile(RuntimeDataEntry data, string profileId = null, bool overwrite = true)
		{
			RuntimeDataEntry write = data.Clone();

			if (profileId == null)
			{
				CurrentProfile.TryAdd(write, overwrite);
				return;
			}
			if (profileId == GLOBAL_DATA_ID)
			{
				GlobalData.TryAdd(write, overwrite);
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
			profile.TryAdd(write, overwrite);
		}

		#endregion Writing

		#region Files

		/// <summary>
		/// Discovers all profile files on disk, keying them by their GUID identity, de-duplicating copies of the
		/// same identity, and (re)builds the metadata index. Files without a GUID identity are ignored.
		/// </summary>
		private void CollectProfiles()
		{
			Profiles.Clear();
			profileFilePaths.Clear();
			_profilesMetaData = new RuntimeDataCollection(GlobalDataIdentifiers.PROFILES);

			foreach (string path in Directory.GetFiles(PROFILES_PATH, "*" + PROFILE_FILE_TYPE))
			{
				string fileName = Path.GetFileNameWithoutExtension(path);
				if (fileName == GLOBAL_DATA_ID || IsIgnoredFileName(fileName))
				{
					continue;
				}

				RuntimeDataCollection profile = SpaxJsonUtils.StreamRead<RuntimeDataCollection>(path);
				if (profile == null)
				{
					SpaxDebug.Error("Couldn't read profile file; skipping.", path);
					continue;
				}

				// Identity is the GUID root ID. Backwards compatibility isn't required, so any file that
				// isn't in the GUID-identity format (legacy/foreign) is ignored rather than migrated.
				if (!Guid.TryParse(profile.ID, out _))
				{
					SpaxDebug.Log("Skipping profile file without a GUID identity (legacy/foreign save):", path);
					continue;
				}

				string id = profile.ID;
				RuntimeDataCollection meta = profile.GetEntry<RuntimeDataCollection>(META_DATA_ID);
				string name = meta?.GetValue<string>(ProfileDataIdentifiers.NAME);

				// De-duplicate identical identities (e.g. a copied save file): the canonical filename wins,
				// otherwise the most recently saved file; the loser is left on disk untouched.
				if (Profiles.ContainsKey(id))
				{
					if (DuplicateReplacesKept(id, name, path, meta))
					{
						SpaxDebug.Error("Duplicate profile - keeping newer/canonical file.", $"now \"{path}\", ignoring \"{profileFilePaths[id]}\"");
					}
					else
					{
						SpaxDebug.Error("Duplicate profile ignored (same identity).", $"kept \"{profileFilePaths[id]}\", ignoring \"{path}\"");
						continue;
					}
				}

				Profiles[id] = null; // Discovered, but not loaded (data is loaded on demand).
				profileFilePaths[id] = path;
				IndexProfileMeta(id, meta);
			}
		}

		/// <summary>
		/// Decides whether a newly-discovered duplicate of an already-indexed identity should replace it.
		/// The canonical "{Name} [{id}]" filename wins; otherwise the most recently saved file wins.
		/// </summary>
		private bool DuplicateReplacesKept(string id, string name, string newPath, RuntimeDataCollection newMeta)
		{
			string keptPath = profileFilePaths[id];
			bool keptCanonical = IsCanonicalFileName(keptPath, name, id);
			bool newCanonical = IsCanonicalFileName(newPath, name, id);
			if (keptCanonical != newCanonical)
			{
				return newCanonical;
			}
			return SaveTimeOf(newMeta) > SaveTimeOf(ProfilesMetaData.GetEntry<RuntimeDataCollection>(id));
		}

		/// <summary>
		/// Adds/overwrites the display index entry for <paramref name="id"/> with a copy of its metadata.
		/// </summary>
		private void IndexProfileMeta(string id, RuntimeDataCollection meta)
		{
			ProfilesMetaData.TryAdd(meta != null ? meta.CloneCollection(id) : new RuntimeDataCollection(id), true);
		}

		/// <summary>
		/// Parses the last-save time out of a metadata collection, or <see cref="DateTime.MinValue"/> if absent/invalid.
		/// </summary>
		private static DateTime SaveTimeOf(RuntimeDataCollection meta)
		{
			if (meta != null && DateTime.TryParse(meta.GetValue<string>(GlobalDataIdentifiers.LAST_SAVE), out DateTime time))
			{
				return time;
			}
			return DateTime.MinValue;
		}

		/// <summary>
		/// Returns the full disk path for the profile with ID <paramref name="profileId"/>, or null if unknown.
		/// The global data profile has a fixed path; player profiles are resolved via <see cref="profileFilePaths"/>.
		/// </summary>
		private string GetProfilePath(string profileId)
		{
			if (profileId == GLOBAL_DATA_ID)
			{
				return PROFILES_PATH + GLOBAL_DATA_ID + GLOBAL_FILE_TYPE;
			}
			return profileFilePaths.TryGetValue(profileId, out string path) ? path : null;
		}

		/// <summary>
		/// Returns the canonical path a profile should be written to given its display name and identity.
		/// </summary>
		private string GetSaveTargetPath(RuntimeDataCollection profile)
		{
			if (profile.ID == GLOBAL_DATA_ID)
			{
				return GetProfilePath(GLOBAL_DATA_ID);
			}
			string name = profile.GetEntry<RuntimeDataCollection>(META_DATA_ID)?.GetValue<string>(ProfileDataIdentifiers.NAME);
			return GetCanonicalProfilePath(name, profile.ID);
		}

		/// <summary>
		/// Builds the canonical, human-readable profile path: "{PROFILES_PATH}{Name} [{id}]{PROFILE_FILE_TYPE}".
		/// </summary>
		private string GetCanonicalProfilePath(string name, string id)
		{
			return PROFILES_PATH + BuildProfileFileName(name, id) + PROFILE_FILE_TYPE;
		}

		/// <summary>
		/// Builds the canonical file name (without extension) for a profile: "{Name} [{id}]".
		/// The name is for humans; the bracketed id is the authoritative identity and guarantees uniqueness.
		/// </summary>
		private static string BuildProfileFileName(string name, string id)
		{
			string prefix = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim() + " ";
			return $"{prefix}[{id}]";
		}

		/// <summary>
		/// Whether the file at <paramref name="path"/> uses the canonical name for the given identity.
		/// </summary>
		private static bool IsCanonicalFileName(string path, string name, string id)
		{
			return string.Equals(Path.GetFileNameWithoutExtension(path), BuildProfileFileName(name, id), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Whether a file named <paramref name="fileName"/> should be skipped during profile collection.
		/// Files parked with a leading '_' or '.' are treated as backups/disabled saves and never loaded as profiles.
		/// </summary>
		private static bool IsIgnoredFileName(string fileName)
		{
			return string.IsNullOrEmpty(fileName) || fileName[0] == '_' || fileName[0] == '.';
		}

		/// <summary>
		/// Returns the loaded (in-memory) profile with ID <paramref name="profileId"/>, or null if it isn't loaded.
		/// </summary>
		private RuntimeDataCollection GetLoadedProfile(string profileId)
		{
			if (CurrentProfile != null && CurrentProfile.ID == profileId)
			{
				return CurrentProfile;
			}
			if (profileId == GLOBAL_DATA_ID)
			{
				return GlobalData;
			}
			return Profiles.TryGetValue(profileId, out RuntimeDataCollection profile) ? profile : null;
		}

		/// <summary>
		/// Migrates a legacy global data file (previously saved with <see cref="PROFILE_FILE_TYPE"/>) to its
		/// dedicated <see cref="GLOBAL_FILE_TYPE"/>, so it is no longer enumerated alongside the profiles.
		/// </summary>
		private void MigrateLegacyGlobalFile()
		{
			string legacyPath = PROFILES_PATH + GLOBAL_DATA_ID + PROFILE_FILE_TYPE;
			string currentPath = PROFILES_PATH + GLOBAL_DATA_ID + GLOBAL_FILE_TYPE;
			if (File.Exists(legacyPath) && !File.Exists(currentPath))
			{
				File.Move(legacyPath, currentPath);
				SpaxDebug.Log("Migrated legacy global data file:", $"{legacyPath} -> {currentPath}");
			}
		}

		#endregion Files

	}
}
