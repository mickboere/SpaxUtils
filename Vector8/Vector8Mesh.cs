using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>Where a stroke sits relative to the edge it strokes (Outside = away from the body material).</summary>
	public enum StrokeAlign { Inside, Center, Outside }

	/// <summary>
	/// Shared mesh maths for the Vector8 UI graphics, all expressed in PERPENDICULAR OFFSET space: a path point offset by
	/// <c>normal * (d * invCos)</c> moves a constant perpendicular distance <c>d</c> from its edges whatever the corner
	/// angle — so thickness and anti-alias width stay uniform instead of thinning at sharp corners.
	///
	/// The pipeline: body/stroke bands are declared as <see cref="Region"/>s, collapsed into contiguous colour
	/// <see cref="Seg"/>ments, then into ±aa transition <see cref="Stop"/>s. Every boundary is one straddling transition —
	/// a COLOUR BLEND between two opaque regions, or an ALPHA FADE against the background. (A transparent fringe on an
	/// internal seam would let the background bleed through as a gap, so internal seams must stay opaque.)
	/// </summary>
	public static class Vector8Mesh
	{
		/// <summary>A drawn band over the perpendicular interval [Lo,Hi]. Higher <see cref="Priority"/> wins on overlap.</summary>
		public readonly struct Region
		{
			public readonly float Lo, Hi;
			public readonly int Priority;
			public readonly Color[] Colors;

			public Region(float lo, float hi, int priority, Color[] colors)
			{
				Lo = lo;
				Hi = hi;
				Priority = priority;
				Colors = colors;
			}
		}

		/// <summary>A contiguous run of one colour after overlaps are resolved.</summary>
		public struct Seg
		{
			public float Lo, Hi;
			public Color[] Colors;

			public Seg(float lo, float hi, Color[] colors)
			{
				Lo = lo;
				Hi = hi;
				Colors = colors;
			}
		}

		/// <summary>One ring of the emitted strip: a perpendicular offset, a colour per path point, and an alpha multiplier.</summary>
		public readonly struct Stop
		{
			public readonly float Offset;
			public readonly Color[] Colors;
			public readonly float Alpha;

			public Stop(float offset, Color[] colors, float alpha)
			{
				Offset = offset;
				Colors = colors;
				Alpha = alpha;
			}
		}

		/// <summary>Outward unit normal of the edge a->b (oriented away from the centre at the origin). Zero if degenerate.</summary>
		public static Vector3 EdgeNormal(Vector3 a, Vector3 b)
		{
			Vector3 d = b - a;
			Vector3 n = new Vector3(d.y, -d.x, 0f); // perpendicular to the edge
			if (n.sqrMagnitude < 1e-8f)
			{
				return Vector3.zero;
			}
			n.Normalize();
			return Vector3.Dot(n, (a + b) * 0.5f) < 0f ? -n : n;
		}

		/// <summary>Per-corner miter direction (bisector of the adjacent edge normals) + 1/cos, for a closed polygon centred on the origin.</summary>
		public static void ComputeMiters(Vector3[] corners, Vector3[] miter, float[] invCos)
		{
			int count = corners.Length;
			for (int i = 0; i < count; i++)
			{
				Vector3 nPrev = EdgeNormal(corners[(i + count - 1) % count], corners[i]);
				Vector3 nNext = EdgeNormal(corners[i], corners[(i + 1) % count]);
				Vector3 m = nPrev + nNext;
				m = m.sqrMagnitude > 1e-6f ? m.normalized : nNext;
				float cos = Mathf.Clamp(Vector3.Dot(m, nNext), 0.25f, 1f); // clamp guards runaway spikes at sharp corners
				miter[i] = m;
				invCos[i] = 1f / cos;
			}
		}

		/// <summary>Adds the stroke band for the edge at <paramref name="edge"/>. <paramref name="outward"/> is the sign of
		/// "away from the body material" at that edge (+1 on an outer edge, -1 on an inner one).</summary>
		public static void AddStroke(List<Region> regions, float edge, float thickness, StrokeAlign align, int outward, Color[] colors)
		{
			float a, b;
			switch (align)
			{
				case StrokeAlign.Outside: a = edge; b = edge + outward * thickness; break;
				case StrokeAlign.Inside: a = edge; b = edge - outward * thickness; break;
				default: a = edge - thickness * 0.5f; b = edge + thickness * 0.5f; break; // Center
			}
			regions.Add(new Region(Mathf.Min(a, b), Mathf.Max(a, b), 2, colors));
		}

		/// <summary>Sweeps the region endpoints into contiguous same-colour segments (inner -> outer); overlaps resolve by
		/// priority. When <paramref name="hasFill"/>, the first segment is the fan (centre -> innermost endpoint).</summary>
		public static List<Seg> BuildSegments(List<Region> regions, bool hasFill, Color[] fillColors)
		{
			List<Seg> segs = new();

			List<float> points = new();
			if (hasFill)
			{
				points.Add(0f); // the path is the fill's outer edge
			}
			foreach (Region r in regions)
			{
				points.Add(r.Lo);
				points.Add(r.Hi);
			}
			points.Sort();

			List<float> ends = new();
			foreach (float p in points)
			{
				if (ends.Count == 0 || Mathf.Abs(p - ends[ends.Count - 1]) > 1e-5f)
				{
					ends.Add(p);
				}
			}
			if (ends.Count == 0)
			{
				return segs;
			}

			float outer = hasFill ? 0f : float.MinValue;
			foreach (Region r in regions)
			{
				outer = Mathf.Max(outer, r.Hi);
			}

			if (hasFill)
			{
				segs.Add(new Seg(ends[0] - 1f, ends[0], fillColors)); // sentinel lo; the centre fan covers it
			}
			for (int k = 0; k < ends.Count - 1; k++)
			{
				float lo = ends[k], hi = ends[k + 1];
				if (hi > outer + 1e-5f)
				{
					break;
				}
				Color[] col = ColorAt(regions, hasFill, fillColors, (lo + hi) * 0.5f);
				if (col == null)
				{
					continue;
				}
				if (segs.Count > 0 && ReferenceEquals(segs[segs.Count - 1].Colors, col) && Mathf.Abs(segs[segs.Count - 1].Hi - lo) < 1e-4f)
				{
					Seg prev = segs[segs.Count - 1];
					prev.Hi = hi;
					segs[segs.Count - 1] = prev; // extend the contiguous same-colour run
				}
				else
				{
					segs.Add(new Seg(lo, hi, col));
				}
			}
			return segs;
		}

		/// <summary>Highest-priority region colour covering <paramref name="d"/>, or the fill colour inside the path, else null.</summary>
		private static Color[] ColorAt(List<Region> regions, bool hasFill, Color[] fillColors, float d)
		{
			Color[] best = null;
			int bestPriority = int.MinValue;
			foreach (Region r in regions)
			{
				if (d > r.Lo + 1e-6f && d < r.Hi - 1e-6f && r.Priority > bestPriority)
				{
					best = r.Colors;
					bestPriority = r.Priority;
				}
			}
			if (best != null)
			{
				return best;
			}
			return hasFill && d < 0f ? fillColors : null;
		}

		/// <summary>Clamps the AA half-width so a band thinner than the fringe can't invert (fringe crossing the solid core).</summary>
		public static float ClampAA(List<Seg> segments, bool hasFill, float halfAA)
		{
			for (int j = 0; j < segments.Count; j++)
			{
				if (hasFill && j == 0)
				{
					continue; // the fan segment reaches the centre; its length isn't a constraint
				}
				halfAA = Mathf.Min(halfAA, (segments[j].Hi - segments[j].Lo) * 0.49f);
			}
			return Mathf.Max(halfAA, 0f);
		}

		/// <summary>Turns segments into ±aa transition stops: a colour blend between two opaque regions, or an alpha fade
		/// against the background (the outer edge, and the inner hole when there's no fill).</summary>
		public static List<Stop> BuildStops(List<Seg> segments, bool hasFill, float aa)
		{
			List<Stop> stops = new();
			int n = segments.Count;
			if (n == 0)
			{
				return stops;
			}

			if (!hasFill)
			{
				stops.Add(new Stop(segments[0].Lo - aa, segments[0].Colors, 0f)); // inner hole: transparent ...
				stops.Add(new Stop(segments[0].Lo + aa, segments[0].Colors, 1f)); // ... to solid
			}
			for (int j = 0; j < n; j++)
			{
				float b = segments[j].Hi;
				if (j < n - 1)
				{
					stops.Add(new Stop(b - aa, segments[j].Colors, 1f));     // colour blend: inner region ...
					stops.Add(new Stop(b + aa, segments[j + 1].Colors, 1f)); // ... to outer region (both opaque, no bleed)
				}
				else
				{
					stops.Add(new Stop(b - aa, segments[j].Colors, 1f)); // outer edge: solid ...
					stops.Add(new Stop(b + aa, segments[j].Colors, 0f)); // ... to transparent
				}
			}
			return stops;
		}

		/// <summary>
		/// Emits the quad strip between consecutive stops along a path. Each stop contributes one ring of
		/// <c>points.Length</c> verts. Returns the first ring's vertex index (so a caller can fan a centre vertex to it).
		/// </summary>
		public static int AppendStrip(VertexHelper vh, Vector3[] points, Vector3[] normals, float[] invCos, bool closed, List<Stop> stops, Color tint)
		{
			int count = points.Length;
			int first = vh.currentVertCount;

			for (int s = 0; s < stops.Count; s++)
			{
				Stop stop = stops[s];
				for (int j = 0; j < count; j++)
				{
					Vector3 pos = points[j] + normals[j] * (stop.Offset * invCos[j]);
					Color c = tint * stop.Colors[j];
					c.a *= stop.Alpha;
					vh.AddVert(pos, c, Vector2.zero);
				}
			}

			int spans = closed ? count : count - 1; // a closed path wraps the last point back to the first
			for (int s = 0; s < stops.Count - 1; s++)
			{
				int a = first + s * count;
				int b = first + (s + 1) * count;
				for (int j = 0; j < spans; j++)
				{
					int next = (j + 1) % count;
					Quad(vh, a + j, b + j, b + next, a + next);
				}
			}
			return first;
		}

		public static void Quad(VertexHelper vh, int a, int b, int c, int d)
		{
			vh.AddTriangle(a, b, c);
			vh.AddTriangle(a, c, d);
		}
	}
}
