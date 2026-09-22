using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A player-adjustable value. The serialized field is only the default; the runtime value is resolved from its store.
	/// </summary>
	[Serializable]
	public abstract class Setting
	{
		/// <summary>
		/// Invoked when the value changed, with the player index (-1 when not per player).
		/// </summary>
		public event Action<Setting, int> ChangedEvent;

		public SettingAttribute Info { get; private set; }
		public string Key => Info?.Key;
		public SettingScope Scope => Info != null ? Info.Scope : SettingScope.Global;
		public bool PerPlayer => Scope == SettingScope.Player;

		[NonSerialized] private ISettingsStore store;

		public abstract bool IsDefault(int player = -1);

		public abstract void ResetToDefault(int player = -1);

		/// <summary>
		/// Untyped access, for code that snapshots and restores values without knowing their type.
		/// </summary>
		public abstract object GetBoxed(int player = -1);

		/// <inheritdoc cref="GetBoxed"/>
		public abstract void SetBoxed(object value, int player = -1);

		/// <summary>
		/// Re-resolves the value from the store and notifies. For per-player settings, -1 reloads every cached player.
		/// </summary>
		public abstract void Reload(int player = -1);

		/// <summary>
		/// Adopts <paramref name="source"/>'s default and reloads. Lets inspector edits on the asset reach its running copy.
		/// </summary>
		public abstract void SyncDefault(Setting source);

		internal void Bind(SettingAttribute info, ISettingsStore store)
		{
			Info = info;
			this.store = store;
			ClearCache();
		}

		protected abstract void ClearCache();

		protected int Slot(int player)
		{
			return PerPlayer ? Mathf.Max(0, player) : -1;
		}

		protected object Read(int slot)
		{
			return store?.Read(this, slot);
		}

		protected void Write(int slot, object data)
		{
			store?.Write(this, slot, data);
		}

		protected void NotifyChanged(int slot)
		{
			ChangedEvent?.Invoke(this, slot);
		}
	}

	/// <inheritdoc/>
	[Serializable]
	public abstract class Setting<T> : Setting
	{
		public T Default => defaultValue;
		public T Value { get => Get(); set => Set(value); }

		[SerializeField] protected T defaultValue;

		[NonSerialized] private bool resolved;
		[NonSerialized] private T value;
		[NonSerialized] private Dictionary<int, T> playerValues;

		protected Setting(T defaultValue)
		{
			this.defaultValue = defaultValue;
		}

		public T Get(int player = -1)
		{
			int slot = Slot(player);
			if (PerPlayer)
			{
				playerValues ??= new Dictionary<int, T>();
				if (!playerValues.TryGetValue(slot, out T v))
				{
					v = Resolve(slot);
					playerValues[slot] = v;
				}
				return v;
			}

			if (!resolved)
			{
				value = Resolve(slot);
				resolved = true;
			}
			return value;
		}

		public void Set(T newValue, int player = -1)
		{
			int slot = Slot(player);
			newValue = Sanitize(newValue);
			if (AreEqual(Get(slot), newValue))
			{
				return;
			}

			Assign(slot, newValue);
			Write(slot, AreEqual(newValue, Sanitize(defaultValue)) ? null : ToData(newValue));
			NotifyChanged(slot);
		}

		public override bool IsDefault(int player = -1)
		{
			return AreEqual(Get(player), Sanitize(defaultValue));
		}

		public override void ResetToDefault(int player = -1)
		{
			Set(defaultValue, player);
			Write(Slot(player), null);
		}

		public override object GetBoxed(int player = -1)
		{
			return Get(player);
		}

		public override void SetBoxed(object value, int player = -1)
		{
			if (value is T typed)
			{
				Set(typed, player);
			}
		}

		public override void Reload(int player = -1)
		{
			if (!PerPlayer)
			{
				resolved = false;
				Get();
				NotifyChanged(-1);
			}
			else if (player >= 0)
			{
				playerValues?.Remove(player);
				Get(player);
				NotifyChanged(player);
			}
			else if (playerValues != null)
			{
				foreach (int cached in new List<int>(playerValues.Keys))
				{
					Reload(cached);
				}
			}
		}

		public override void SyncDefault(Setting source)
		{
			if (source is Setting<T> typed)
			{
				defaultValue = typed.defaultValue;
				Reload();
			}
		}

		protected override void ClearCache()
		{
			resolved = false;
			playerValues?.Clear();
		}

		/// <summary>
		/// Converts the value to something the runtime data can serialize.
		/// </summary>
		protected virtual object ToData(T value)
		{
			return value;
		}

		protected virtual bool TryFromData(object data, out T value)
		{
			if (data is T cast)
			{
				value = cast;
				return true;
			}
			value = default;
			return false;
		}

		protected virtual T Sanitize(T value)
		{
			return value;
		}

		protected virtual bool AreEqual(T a, T b)
		{
			return EqualityComparer<T>.Default.Equals(a, b);
		}

		private T Resolve(int slot)
		{
			object data = Read(slot);
			return Sanitize(data != null && TryFromData(data, out T stored) ? stored : defaultValue);
		}

		private void Assign(int slot, T newValue)
		{
			if (PerPlayer)
			{
				playerValues[slot] = newValue;
			}
			else
			{
				value = newValue;
				resolved = true;
			}
		}
	}
}
