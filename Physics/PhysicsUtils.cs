using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public static class PhysicsUtils
	{
		public const float PI2 = Mathf.PI * 2f;

		/// <summary>
		/// Starting at <paramref name="origin"/>,
		/// Shoots <paramref name="rays"/> amount of rays,
		/// In a circle of <paramref name="radius"/>,
		/// Pointed in <paramref name="direction"/>,
		/// With a max distance of <paramref name="maxLength"/>.
		/// </summary>
		/// <param name="origin">Cast origin.</param>
		/// <param name="radius">Tube radius.</param>
		/// <param name="direction">Tube direction.</param>
		/// <param name="maxLength">Max hit distance of tube.</param>
		/// <param name="layermask">Which layers can get hit.</param>
		/// <param name="rays">Amount of rays to devide the tube into.</param>
		/// <param name="hits">The resulting hits from the cast.</param>
		/// <param name="sort">Sorts the resulting hits by distance.</param>
		/// <returns>True if there were any hits, false if there were not.</returns>
		public static bool TubeCast(Vector3 origin, float radius, Vector3 direction, Vector3 up, float maxLength, int layermask,
			int rays, out List<RaycastHit> hits, bool sort = false)
		{
			hits = new List<RaycastHit>();
			for (int i = 0; i < rays; i++)
			{
				float t = (float)i / rays;
				Vector3 from = origin.Circle(radius, t, direction, up);
				if (Physics.Raycast(from, direction, out RaycastHit hit, maxLength, layermask))
				{
					hits.Add(hit);
				}
			}
			if (sort)
			{
				hits.Sort((a, b) => a.distance.CompareTo(b.distance));
			}
			return hits.Count > 0;
		}

		/// <summary>
		/// Starting at <paramref name="origin"/>,
		/// Shoots <paramref name="rays"/> amount of rays,
		/// In a circle of <paramref name="scale"/>,
		/// Pointed in <paramref name="direction"/>,
		/// With a max distance of <paramref name="maxLength"/>.
		/// </summary>
		/// <param name="origin">Cast origin.</param>
		/// <param name="scale">Tube radius.</param>
		/// <param name="direction">Tube direction.</param>
		/// <param name="maxLength">Max hit distance of tube.</param>
		/// <param name="layermask">Which layers can get hit.</param>
		/// <param name="rays">Amount of rays to devide the tube into.</param>
		/// <param name="hits">The resulting hits from the cast.</param>
		/// <param name="sort">Sorts the resulting hits by distance.</param>
		/// <returns>True if there were any hits, false if there were not.</returns>
		public static bool TubeCast(Vector3 origin, Vector3 scale, Vector3 direction, Vector3 up, float maxLength, int layermask,
			int rays, out List<RaycastHit> hits, bool sort = false)
		{
			hits = new List<RaycastHit>();
			for (int i = 0; i < rays; i++)
			{
				float t = (float)i / rays;
				Vector3 from = origin.Circle(scale, t, direction, up);
				if (Physics.Raycast(from, direction, out RaycastHit hit, maxLength, layermask))
				{
					hits.Add(hit);
				}
			}
			if (sort)
			{
				hits.Sort((a, b) => a.distance.CompareTo(b.distance));
			}
			return hits.Count > 0;
		}
	}
}
