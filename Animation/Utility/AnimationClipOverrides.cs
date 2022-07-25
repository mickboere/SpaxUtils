using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class used to set many animation clip overrides at once.
	/// </summary>
	/// Taken from https://docs.unity3d.com/ScriptReference/AnimatorOverrideController.html
	public class AnimationClipOverrides : List<KeyValuePair<AnimationClip, AnimationClip>>
	{
		public AnimationClip this[string name]
		{
			get
			{
				return Find(x => x.Key.name.Equals(name)).Value;
			}
			set
			{
				int index = FindIndex(x => x.Key.name.Equals(name));
				if (index != -1)
				{
					this[index] = new KeyValuePair<AnimationClip, AnimationClip>(this[index].Key, value);
				}
			}
		}

		public AnimationClipOverrides(int capacity) : base(capacity) { }
	}
}
