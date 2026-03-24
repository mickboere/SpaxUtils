using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Serializable data describing how to set a single flag: clear and complete settings.
	/// Does NOT include setID; that is context-dependent and left to the caller.
	/// </summary>
	[Serializable]
	public struct FlagSetting
	{
		[SerializeField, ConstDropdown(typeof(IFlags), inputField: true)] public string flag;
		[SerializeField, Conditional(nameof(clear), true), Tooltip("Mark the flag as being completed.")] public bool complete;
		[SerializeField, Tooltip("Clears the flag data from the profile.")] public bool clear;

		/// <summary>
		/// Applies this flag setting through the <paramref name="flagService"/>.
		/// </summary>
		/// <param name="flagService">The flag service to apply the setting through.</param>
		/// <param name="setterId">Optional setter ID. Ignored when <see cref="clear"/> is true.</param>
		public void Apply(FlagService flagService, string setterId = "")
		{
			string[] arr = new string[] { flag };
			if (clear)
			{
				flagService.ClearFlags(arr);
			}
			else
			{
				flagService.SetFlags(arr, setterId, complete, true);
			}
		}

		/// <summary>
		/// Applies all <paramref name="settings"/> through the <paramref name="flagService"/>.
		/// </summary>
		public static void ApplyAll(FlagSetting[] settings, FlagService flagService, string setterId = "")
		{
			if (settings == null)
			{
				return;
			}

			for (int i = 0; i < settings.Length; i++)
			{
				settings[i].Apply(flagService, setterId);
			}
		}
	}
}
