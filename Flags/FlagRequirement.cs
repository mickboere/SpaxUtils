using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	/// <summary>
	/// Serializable data describing a single flag requirement: which flag, whether completion is needed, and whether to invert the check.
	/// </summary>
	[Serializable]
	public struct FlagRequirement
	{
		[SerializeField, ConstDropdown(typeof(IFlags), inputField: true)] public string flag;
		[SerializeField, FormerlySerializedAs("requireCompletion")] public bool completed;
		[SerializeField] public bool invert;

		/// <summary>
		/// Evaluates this single requirement against the <paramref name="flagService"/>.
		/// </summary>
		public bool Evaluate(FlagService flagService)
		{
			string[] arr = new string[] { flag };
			bool has = completed ? flagService.HasCompletedFlags(arr) : flagService.HasFlags(arr);
			return has != invert;
		}

		/// <summary>
		/// Returns true only if ALL <paramref name="requirements"/> are met.
		/// Returns true when the array is null or empty (no requirements = always met).
		/// </summary>
		public static bool EvaluateAll(FlagRequirement[] requirements, FlagService flagService)
		{
			if (requirements == null || requirements.Length == 0)
			{
				return true;
			}

			for (int i = 0; i < requirements.Length; i++)
			{
				if (!requirements[i].Evaluate(flagService))
				{
					return false;
				}
			}

			return true;
		}
	}
}
