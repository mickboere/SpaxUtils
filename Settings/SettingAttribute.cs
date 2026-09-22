using System;

namespace SpaxUtils
{
	/// <summary>
	/// Exposes a <see cref="Setting"/> field to the <see cref="SettingsService"/> and the settings screen.
	/// Code-constant on purpose: the key must never change once shipped, or players lose their override.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class SettingAttribute : Attribute
	{
		public string Key { get; }
		public string Tab { get; }
		public string Section { get; }
		public string Label { get; }

		public SettingScope Scope { get; set; } = SettingScope.Global;
		public float Min { get; set; }
		public float Max { get; set; }

		/// <summary>
		/// Numeric display format, e.g. "P0" to show 0..1 as a percentage.
		/// </summary>
		public string Format { get; set; }

		/// <summary>
		/// Changes must be confirmed by the player or they revert, for settings that can leave the screen unusable.
		/// </summary>
		public bool Confirm { get; set; }

		public bool HasRange => Max > Min;

		/// <param name="path">"Tab/Section".</param>
		public SettingAttribute(string key, string path, string label)
		{
			Key = key;
			Label = label;
			int split = path.IndexOf('/');
			Tab = split < 0 ? path : path.Substring(0, split);
			Section = split < 0 ? string.Empty : path.Substring(split + 1);
		}

		public SettingAttribute(string key, string path, string label, float min, float max) : this(key, path, label)
		{
			Min = min;
			Max = max;
		}
	}
}
