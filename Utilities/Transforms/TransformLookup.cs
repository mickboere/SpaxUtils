using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Class used for finding child transforms through name.
	/// Also has support for <see cref="HumanBodyBones"/>.
	/// </summary>
	public class TransformLookup : MonoBehaviour, IDependency
	{
		private static List<string> humanBonesCache;

		private Dictionary<string, Transform> cache = new Dictionary<string, Transform>();

		private Animator animator;

		public void InjectDependencies(AnimatorWrapper animatorWrapper)
		{
			animator = animatorWrapper.Animator;
		}

		/// <summary>
		/// Find transform using <paramref name="identifier"/>.
		/// </summary>
		/// <param name="identifier">The identifier of the bone. Could be a human bone name, or a transform name.</param>
		/// <returns>The transform mapped to <paramref name="identifier"/>, null if it couldn't be found.</returns>
		public Transform Lookup(string identifier)
		{
			if (cache.ContainsKey(identifier))
			{
				// This transform was found before.
				return cache[identifier];
			}

			Transform result = null;

			// Check by child transform name.
			result = transform.FindChildRecursive(identifier);

			// Check for human mappable bones.
			if (result == null)
			{
				result = GetHumanBone(identifier);
			}

			// Cache the result if we found something.
			if (result != null)
			{
				cache.Add(identifier, result);
			}

			return result;
		}

		private Transform GetHumanBone(string bone)
		{
			// Ensure the human bones collection has been cached.
			if (humanBonesCache == null)
			{
				humanBonesCache = typeof(HumanBoneIdentifiers).GetAllPublicConstStrings(false);
			}

			// Ensure we have an animator to retrieve the bones from.
			EnsureAnimator();
			if (animator == null)
			{
				return null;
			}

			// Confirm the bone is a human bone.
			if (humanBonesCache.Contains(bone))
			{
				// Try to parse the bone to a HumanBodyBones enum.
				if (Enum.TryParse(bone, out HumanBodyBones id))
				{
					return animator.GetBoneTransform(id);
				}
				else
				{
					SpaxDebug.Error("Could not parse bone:", bone, this);
				}
			}

			return null;
		}

		private void EnsureAnimator()
		{
			if (animator == null)
			{
				animator = gameObject.GetComponent<Animator>();
				if (animator == null)
				{
					AnimatorWrapper wrapper = gameObject.GetComponent<AnimatorWrapper>();
					if (wrapper != null)
					{
						animator = wrapper.Animator;
					}
				}
			}
		}
	}
}
