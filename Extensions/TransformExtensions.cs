using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Extensions for <see cref="Transform"/>.
	/// </summary>
	public static class TransformExtensions
	{
		/// <summary>
		/// Returns a path string towards <paramref name="current"/>.
		/// </summary>
		/// <param name="current"></param>
		/// <returns>A path string towards <paramref name="current"/>.</returns>
		public static string GetPath(this Transform current)
		{
			if (current.parent == null)
			{
				return "/" + current.name;
			}
			return current.parent.GetPath() + "/" + current.name;
		}

		/// <summary>
		/// Recursively checks whether any of <paramref name="transform"/>'s parents is <paramref name="parent"/>.
		/// </summary>
		public static bool HasParent(this Transform transform, Transform parent)
		{
			if (transform == parent || transform.parent == parent)
			{
				return true;
			}
			else if (transform.parent != null)
			{
				return transform.parent.HasParent(parent);
			}

			return false;
		}

		/// <summary>
		/// Will recursively look for a child in <paramref name="current"/> named <paramref name="name"/>.
		/// </summary>
		/// <param name="current">The transform to recursively look into.</param>
		/// <param name="name">The name of the child we're looking for.</param>
		/// <returns>If found, a child of name <paramref name="name"/>.</returns>
		public static Transform FindChildRecursive(this Transform current, string name)
		{
			for (int i = 0; i < current.childCount; i++)
			{
				// Direct children
				Transform child = current.GetChild(i);
				if (child.name == name)
				{
					return child;
				}

				// Recurse
				Transform find = FindChildRecursive(child, name);
				if (find != null)
				{
					return find;
				}
			}

			return null;
		}

		/// <summary>
		/// Recursively collect all children from <paramref name="parent"/>.
		/// </summary>
		/// <param name="parent">The parent from which to collect all children.</param>
		/// <param name="predicate">Optional func to filter which children get added.</param>
		/// <param name="includeParent">Whether the <paramref name="parent"/> should be included in the list of children.</param>
		/// <returns>A (filtered) list of <paramref name="parent"/>'s children and children's children.</returns>
		public static List<Transform> CollectChildrenRecursive(this Transform parent, Func<Transform, bool> predicate = null, bool includeParent = false)
		{
			List<Transform> children = new List<Transform>();
			if (includeParent)
			{
				children.Add(parent);
			}
			for (int i = 0; i < parent.childCount; i++)
			{
				Transform child = parent.GetChild(i);
				if (predicate != null && !predicate(child))
				{
					continue;
				}
				children.Add(child);
				children.AddRange(child.CollectChildrenRecursive(predicate));
			}
			return children;
		}

		/// <summary>
		/// Retrieves the center of weight for <paramref name="transforms"/>.
		/// Ignores transforms of which their position is identical to their parent.
		/// </summary>
		public static Vector3 GetCenter(this IReadOnlyCollection<Transform> transforms, Func<Transform, float> weightFunc = null)
		{
			return Vector3Extensions.AveragePoint(
				transforms.Select(t => t.position).ToArray(),
				transforms.Select(t =>
					t.parent != null && t.position == t.parent.position ? 0f :
					(weightFunc == null ? 1f : weightFunc(t))).ToArray());
		}
	}
}
