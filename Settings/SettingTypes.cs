using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// A setting presented as a list of labelled options (dropdown).
	/// </summary>
	public interface IOptionSetting
	{
		IReadOnlyList<string> GetLabels();

		/// <summary>
		/// Index of the current value within <see cref="GetLabels"/>, or -1 when it isn't listed.
		/// </summary>
		int GetIndex(int player = -1);

		void SetIndex(int index, int player = -1);
	}

	[Serializable]
	public class FloatSetting : Setting<float>
	{
		public FloatSetting() : base(0f) { }
		public FloatSetting(float defaultValue) : base(defaultValue) { }

		protected override float Sanitize(float value)
		{
			return Info != null && Info.HasRange ? Mathf.Clamp(value, Info.Min, Info.Max) : value;
		}

		protected override bool AreEqual(float a, float b)
		{
			return Mathf.Approximately(a, b);
		}

		protected override bool TryFromData(object data, out float value)
		{
			return SettingData.TryConvert(data, out value);
		}
	}

	[Serializable]
	public class IntSetting : Setting<int>
	{
		public IntSetting() : base(0) { }
		public IntSetting(int defaultValue) : base(defaultValue) { }

		protected override int Sanitize(int value)
		{
			return Info != null && Info.HasRange ? Mathf.Clamp(value, (int)Info.Min, (int)Info.Max) : value;
		}

		protected override bool TryFromData(object data, out int value)
		{
			return SettingData.TryConvert(data, out value);
		}
	}

	[Serializable]
	public class BoolSetting : Setting<bool>
	{
		public BoolSetting() : base(false) { }
		public BoolSetting(bool defaultValue) : base(defaultValue) { }
	}

	/// <summary>
	/// Stored as int, since runtime data reads enums back as int.
	/// </summary>
	[Serializable]
	public class EnumSetting<TEnum> : Setting<TEnum>, IOptionSetting where TEnum : struct, Enum
	{
		private static readonly TEnum[] values = (TEnum[])Enum.GetValues(typeof(TEnum));
		private static readonly string[] labels = Array.ConvertAll(values, GetLabel);

		public EnumSetting() : base(default) { }
		public EnumSetting(TEnum defaultValue) : base(defaultValue) { }

		// Unity's InspectorName doubles as the label, for names an identifier can't spell ("&", ",").
		private static string GetLabel(TEnum value)
		{
			InspectorNameAttribute name =
				typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<InspectorNameAttribute>();
			return name != null ? name.displayName : value.ToString().Nicify();
		}

		public IReadOnlyList<string> GetLabels()
		{
			return labels;
		}

		public int GetIndex(int player = -1)
		{
			return Array.IndexOf(values, Get(player));
		}

		public void SetIndex(int index, int player = -1)
		{
			if (index >= 0 && index < values.Length)
			{
				Set(values[index], player);
			}
		}

		protected override object ToData(TEnum value)
		{
			return Convert.ToInt32(value);
		}

		protected override bool TryFromData(object data, out TEnum value)
		{
			if (SettingData.TryConvert(data, out int i) && Enum.IsDefined(typeof(TEnum), i))
			{
				value = (TEnum)Enum.ToObject(typeof(TEnum), i);
				return true;
			}
			value = default;
			return false;
		}
	}

	/// <summary>
	/// A string value picked from options supplied at runtime (resolutions, monitors, ...).
	/// Stores the value, not the index, so it survives hardware changes.
	/// </summary>
	[Serializable]
	public class ChoiceSetting : Setting<string>, IOptionSetting
	{
		public readonly struct Option
		{
			public readonly string Value;
			public readonly string Label;

			public Option(string value, string label = null)
			{
				Value = value ?? string.Empty;
				Label = label ?? value;
			}
		}

		[NonSerialized] private Func<IReadOnlyList<Option>> provider;

		public ChoiceSetting() : base(string.Empty) { }
		public ChoiceSetting(string defaultValue) : base(defaultValue) { }

		public void SetOptionProvider(Func<IReadOnlyList<Option>> provider)
		{
			this.provider = provider;
		}

		public IReadOnlyList<Option> GetOptions()
		{
			return provider?.Invoke() ?? Array.Empty<Option>();
		}

		public IReadOnlyList<string> GetLabels()
		{
			IReadOnlyList<Option> options = GetOptions();
			string[] result = new string[options.Count];
			for (int i = 0; i < options.Count; i++)
			{
				result[i] = options[i].Label;
			}
			return result;
		}

		public int GetIndex(int player = -1)
		{
			IReadOnlyList<Option> options = GetOptions();
			string current = Get(player);
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i].Value == current)
				{
					return i;
				}
			}
			return -1;
		}

		public void SetIndex(int index, int player = -1)
		{
			IReadOnlyList<Option> options = GetOptions();
			if (index >= 0 && index < options.Count)
			{
				Set(options[index].Value, player);
			}
		}

		protected override string Sanitize(string value)
		{
			return value ?? string.Empty;
		}
	}

	/// <summary>
	/// Input binding overrides as JSON; the settings screen expands it into one row per rebindable binding.
	/// </summary>
	[Serializable]
	public class BindingsSetting : Setting<string>
	{
		public BindingsSetting() : base(string.Empty) { }

		protected override string Sanitize(string value)
		{
			return value ?? string.Empty;
		}
	}

	internal static class SettingData
	{
		public static bool TryConvert<T>(object data, out T value) where T : IConvertible
		{
			try
			{
				if (data is IConvertible)
				{
					value = (T)Convert.ChangeType(data, typeof(T), CultureInfo.InvariantCulture);
					return true;
				}
			}
			catch (Exception)
			{
				// Corrupt or foreign data falls back to the default.
			}
			value = default;
			return false;
		}
	}
}
