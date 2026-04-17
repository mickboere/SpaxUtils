using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Material override mapping.
	/// - If there is exactly 1 override and Source is null, Target replaces ALL materials on the instance.
	/// - If there are multiple overrides, ALL overrides must have a Source; if any Source is null, nothing is applied.
	/// </summary>
	[Serializable]
	public struct MaterialOverride
	{
		public Material Source;
		public Material Target;

		/// <summary>
		/// Apply the given overrides to all renderers in <paramref name="instance"/>.
		/// </summary>
		public static void ApplyOverrides(GameObject instance, IReadOnlyList<MaterialOverride> overrides)
		{
			if (instance == null) return;
			if (overrides == null || overrides.Count == 0) return;

			// Rule 1: Exactly one override with no source selected -> replace ALL materials with the target.
			if (overrides.Count == 1)
			{
				MaterialOverride o = overrides[0];
				if (o.Target == null) return;

				if (o.Source == null)
				{
					ReplaceAll(instance, o.Target);
				}
				else
				{
					ReplaceByMap(instance, new Dictionary<Material, Material> { { o.Source, o.Target } });
				}

				return;
			}

			// Rule 2: Multiple overrides -> all MUST have a Source, otherwise apply nothing.
			for (int i = 0; i < overrides.Count; i++)
			{
				if (overrides[i].Source == null)
				{
					return;
				}
			}

			// Build mapping (skip null targets).
			Dictionary<Material, Material> map = new Dictionary<Material, Material>();
			for (int i = 0; i < overrides.Count; i++)
			{
				MaterialOverride o = overrides[i];
				if (o.Target == null) continue;

				// Last one wins if duplicates exist.
				map[o.Source] = o.Target;
			}

			if (map.Count == 0) return;
			ReplaceByMap(instance, map);
		}

		private static void ReplaceAll(GameObject instance, Material target)
		{
			if (target == null) return;

			Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
			for (int r = 0; r < renderers.Length; r++)
			{
				Renderer renderer = renderers[r];
				if (renderer == null) continue;

				Material[] src = renderer.sharedMaterials;
				if (src == null || src.Length == 0) continue;

				bool changed = false;
				Material[] dst = (Material[])src.Clone();

				for (int m = 0; m < dst.Length; m++)
				{
					if (dst[m] != target)
					{
						dst[m] = target;
						changed = true;
					}
				}

				if (changed)
				{
					renderer.sharedMaterials = dst;
				}
			}
		}

		private static void ReplaceByMap(GameObject instance, Dictionary<Material, Material> map)
		{
			Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
			for (int r = 0; r < renderers.Length; r++)
			{
				Renderer renderer = renderers[r];
				if (renderer == null) continue;

				Material[] src = renderer.sharedMaterials;
				if (src == null || src.Length == 0) continue;

				bool changed = false;
				Material[] dst = (Material[])src.Clone();

				for (int m = 0; m < dst.Length; m++)
				{
					Material current = dst[m];
					if (current != null && map.TryGetValue(current, out Material replacement) && replacement != null && replacement != current)
					{
						dst[m] = replacement;
						changed = true;
					}
				}

				if (changed)
				{
					renderer.sharedMaterials = dst;
				}
			}
		}
	}
}
