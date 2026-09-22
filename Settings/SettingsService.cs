using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Collects every <see cref="SettingAttribute"/> field on the <see cref="ISettingsSupplier"/> services and stores their overrides.
	/// Only non-default values are stored, so retuned defaults reach everyone who never touched them.
	/// </summary>
	public class SettingsService : IService, IInitializable, IDisposable, ISettingsStore
	{
		private const string PLAYER_PREFIX = "P";

		/// <summary>
		/// Invoked when any registered setting changed, with the player index (-1 when not per player).
		/// </summary>
		public event Action<Setting, int> SettingChangedEvent;

		/// <summary>
		/// All registered settings, in registration and declaration order.
		/// </summary>
		public IReadOnlyList<Setting> Settings => settings;

		private readonly RuntimeDataService runtimeDataService;
		private readonly IDependencyManager dependencyManager;
		private readonly List<Setting> settings = new List<Setting>();
		private readonly Dictionary<string, Setting> settingsByKey = new Dictionary<string, Setting>();
		private readonly HashSet<ISettingsSupplier> suppliers = new HashSet<ISettingsSupplier>();
		private bool dirty;

		public SettingsService(RuntimeDataService runtimeDataService, IDependencyManager dependencyManager)
		{
			this.runtimeDataService = runtimeDataService;
			this.dependencyManager = dependencyManager;
		}

		public void Initialize()
		{
			runtimeDataService.CurrentProfileChangedEvent += OnCurrentProfileChanged;

			// Suppliers are lazy services; create them all so every setting applies from boot.
			foreach (Type type in typeof(ISettingsSupplier).GetAllImplementations())
			{
				if (typeof(IService).IsAssignableFrom(type) &&
					dependencyManager.Get(type, type) is ISettingsSupplier supplier)
				{
					Register(supplier);
				}
			}
		}

		public void Dispose()
		{
			runtimeDataService.CurrentProfileChangedEvent -= OnCurrentProfileChanged;
			foreach (Setting setting in settings)
			{
				setting.ChangedEvent -= OnSettingChanged;
			}
			Save();
			RuntimeAssetGuard.RestoreAll();
		}

		/// <summary>
		/// Registers all settings of <paramref name="supplier"/> and applies their stored values.
		/// </summary>
		public void Register(ISettingsSupplier supplier)
		{
			if (!suppliers.Add(supplier))
			{
				return;
			}

			const BindingFlags FLAGS =
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
			for (Type type = supplier.GetType(); type != null && type != typeof(object); type = type.BaseType)
			{
				foreach (FieldInfo field in type.GetFields(FLAGS))
				{
					SettingAttribute info = field.GetCustomAttribute<SettingAttribute>();
					if (info == null)
					{
						continue;
					}

					if (field.GetValue(supplier) is not Setting setting)
					{
						SpaxDebug.Error("Setting field is null or not a Setting.", $"{type.Name}.{field.Name} ({info.Key})");
						continue;
					}
					if (settingsByKey.ContainsKey(info.Key))
					{
						SpaxDebug.Error("Duplicate setting key.", $"{info.Key} on {type.Name}.{field.Name}");
						continue;
					}

					setting.Bind(info, this);
					setting.ChangedEvent += OnSettingChanged;
					settingsByKey.Add(info.Key, setting);
					settings.Add(setting);
					setting.Reload();
				}
			}
		}

		public bool TryGet(string key, out Setting setting)
		{
			return settingsByKey.TryGetValue(key, out setting);
		}

		/// <summary>
		/// Writes pending global and per-player overrides to disk. Profile overrides persist with the next game save.
		/// </summary>
		public void Save()
		{
			if (dirty && runtimeDataService.GlobalData != null)
			{
				runtimeDataService.SaveProfileToDisk(RuntimeDataService.GLOBAL_DATA_ID);
				dirty = false;
			}
		}

		#region ISettingsStore

		object ISettingsStore.Read(Setting setting, int player)
		{
			return GetCollection(setting.Scope, player, false)?.GetValue(setting.Key);
		}

		void ISettingsStore.Write(Setting setting, int player, object data)
		{
			RuntimeDataCollection collection = GetCollection(setting.Scope, player, data != null);
			if (collection == null)
			{
				return;
			}

			if (data == null)
			{
				collection.TryRemove(setting.Key, true);
			}
			else
			{
				collection.SetValue(setting.Key, data);
			}

			if (setting.Scope != SettingScope.Profile)
			{
				dirty = true;
			}
		}

		#endregion ISettingsStore

		private RuntimeDataCollection GetCollection(SettingScope scope, int player, bool create)
		{
			bool profile = scope == SettingScope.Profile;
			RuntimeDataCollection root = profile ? runtimeDataService.CurrentProfile : runtimeDataService.GlobalData;
			string id = profile ? ProfileDataIdentifiers.SETTINGS : GlobalDataIdentifiers.SETTINGS;
			RuntimeDataCollection collection = GetChild(root, id, create);
			return scope == SettingScope.Player ? GetChild(collection, PLAYER_PREFIX + player, create) : collection;
		}

		private static RuntimeDataCollection GetChild(RuntimeDataCollection parent, string id, bool create)
		{
			if (parent == null)
			{
				return null;
			}
			return create ?
				parent.GetEntry(id, new RuntimeDataCollection(id)) :
				parent.GetEntry<RuntimeDataCollection>(id);
		}

		private void OnCurrentProfileChanged(RuntimeDataCollection profile)
		{
			foreach (Setting setting in settings)
			{
				if (setting.Scope == SettingScope.Profile)
				{
					setting.Reload();
				}
			}
		}

		private void OnSettingChanged(Setting setting, int player)
		{
			SettingChangedEvent?.Invoke(setting, player);
		}
	}
}
