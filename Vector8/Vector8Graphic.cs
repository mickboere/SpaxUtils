using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Renders a <see cref="Vector8"/> as an 8-sided polygon directly into the UI mesh, with per-corner vertex colors.
	/// Each corner is pushed outwards by its channel value; the GPU interpolates the corner colors to form a gradient loop.
	/// Supports a filled (triangle-fan) mode and an outlined (quad-ring) mode via <see cref="fillMode"/>, an optional
	/// <see cref="stroke"/> border over either, and a transparent <see cref="antiAlias"/> fringe to soften the silhouette.
	/// All edges (body, stroke, AA) map through one perpendicular-offset compositor so thickness and fringe stay uniform.
	/// </summary>
	[ExecuteInEditMode]
	public class Vector8Graphic : MaskableGraphic, IVector8Visualizer
	{
		public enum FillMode { Filled, Outline }
		public enum StrokeAlign { Inside, Center, Outside }

		[Header("Data")]
		[SerializeField] private Vector8 vector8 = Vector8.Half;
		[SerializeField] private float scale = 1f;
		[SerializeField] private bool normalize;
		[SerializeField, Conditional(nameof(normalize), true)] private float range = 1f;

		[Header("Shape")]
		[SerializeField] private FillMode fillMode = FillMode.Filled;
		[SerializeField, Conditional(nameof(fillMode), (int)FillMode.Outline)] private float thickness = 4f;
		[Tooltip("Minimum corner radius (0-1 of half-rect) so zero-value channels still form a visible polygon.")]
		[SerializeField, Range(0f, 1f)] private float floor = 0f;
		[Tooltip("Width in pixels of the transparent fade fringe around the silhouette. 0 = hard edges.")]
		[SerializeField] private float antiAlias = 1.5f;

		[Header("Color")]
		[Tooltip("When off, the body uses the flat 'Body Color' instead of the per-corner gradient — for a plain single-colour shape without editing 8 entries. Also ignores colors passed to Visualize().")]
		[SerializeField] private bool useCornerColors = true;
		[Tooltip("Flat body colour, used when Corner Colors is off. Kept separate from the graphic's own 'Color' so that tint (and its alpha) can stay neutral and not bleed into the stroke.")]
		[SerializeField, Conditional(nameof(useCornerColors), true)] private Color bodyColor = Color.white;
		[Tooltip("Per-corner colors (N, NE, E, SE, S, SW, W, NW). The graphic's own 'color' tints all of them.")]
		[SerializeField, Conditional(nameof(useCornerColors))]
		private Color[] cornerColors = new Color[8]
		{
			Color.white, Color.white, Color.white, Color.white,
			Color.white, Color.white, Color.white, Color.white
		};
		[Tooltip("Filled mode only: color of the central vertex the fan radiates from.")]
		[SerializeField, Conditional(nameof(fillMode), (int)FillMode.Filled)] private Color centerColor = Color.white;

		[Header("Stroke")]
		[Tooltip("Draws a border over the body. Filled mode strokes the silhouette edge; Outline mode strokes BOTH edges of the line. Independent of Fill Mode — toggle per layer to highlight it.")]
		[SerializeField] private bool stroke;
		[Tooltip("Border width, in the same units as Thickness / Anti Alias.")]
		[SerializeField, Conditional(nameof(stroke))] private float strokeThickness = 4f;
		[Tooltip("Where the border sits relative to the edge it strokes: Inside, Center, or Outside (away from the body material). In Outline mode this applies to both edges of the line.")]
		[SerializeField, Conditional(nameof(stroke))] private StrokeAlign strokeAlign = StrokeAlign.Outside;
		[Tooltip("Base the border on the per-corner gradient (like the body) instead of a flat colour. Stroke Color still tints it either way.")]
		[SerializeField, Conditional(nameof(stroke))] private bool strokeUseBodyColors;
		[Tooltip("Border colour. Tints the per-corner gradient when Stroke Use Body Colors is on; otherwise it IS the flat border colour.")]
		[SerializeField, Conditional(nameof(stroke))] private Color strokeColor = Color.white;

		/// <summary>Runtime toggle for the border overlay (e.g. to spotlight one layer).</summary>
		public bool Stroke { get => stroke; set { stroke = value; SetVerticesDirty(); } }
		/// <summary>Runtime flat border colour.</summary>
		public Color StrokeColor { get => strokeColor; set { strokeColor = value; SetVerticesDirty(); } }

		private Color[] activeColors;

		/// <inheritdoc/>
		public void Visualize(Vector8 v, Color[] colors = null)
		{
			vector8 = v;
			activeColors = colors;
			SetVerticesDirty();
		}

		protected void Update()
		{
			if (!Application.isPlaying)
			{
				// Live preview of the serialized value in edit mode.
				SetVerticesDirty();
			}
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();

			Vector8 v = normalize ? vector8.Absolute().NormalizeMax() : vector8.Absolute() / Mathf.Max(range, Mathf.Epsilon);
			if (floor > 0f)
			{
				v = v.Maximize(Vector8.One * floor);
			}

			Vector3[] corners = v.GetPositions3DRect(scale, rectTransform.rect);

			// Per-corner MITER dir + 1/cos: offsetting a vertex by miter*(d/cos) shifts it a CONSTANT perpendicular distance d
			// from the silhouette edge, whatever the neighbour angle. Every band edge and AA fringe uses this same map, so
			// thickness and anti-alias width stay uniform around the shape (no thinning where adjacent corners differ in radius).
			Vector3[] miter = new Vector3[8];
			float[] invCos = new float[8];
			for (int i = 0; i < 8; i++)
			{
				Vector3 nPrev = EdgeNormal(corners[(i + 7) % 8], corners[i]);
				Vector3 nNext = EdgeNormal(corners[i], corners[(i + 1) % 8]);
				Vector3 m = nPrev + nNext;
				m = m.sqrMagnitude > 1e-6f ? m.normalized : nNext;
				float cos = Mathf.Clamp(Vector3.Dot(m, nNext), 0.25f, 1f); // clamp guards runaway spikes at sharp corners
				miter[i] = m;
				invCos[i] = 1f / cos;
			}

			Color[] body;
			if (useCornerColors)
			{
				body = activeColors != null && activeColors.Length >= 8 ? activeColors : cornerColors;
			}
			else
			{
				body = new Color[8];
				for (int i = 0; i < 8; i++) { body[i] = bodyColor; }
			}

			// strokeColor always applies: as a tint over the body gradient, or as the flat border colour on its own.
			Color[] strokeCols = new Color[8];
			for (int i = 0; i < 8; i++)
			{
				strokeCols[i] = strokeUseBodyColors ? body[i] * strokeColor : strokeColor;
			}

			bool hasFill = fillMode == FillMode.Filled;
			float t = Mathf.Max(thickness, 0f);
			float st = Mathf.Max(strokeThickness, 0f);

			// Drawn regions as perpendicular-offset intervals [lo,hi] (path = 0, + = outward), with a priority (higher wins on
			// overlap). Fill isn't a band — it's the central fan colouring everything inside the path (handled in BuildSegments).
			List<Region> regions = new();
			if (!hasFill)
			{
				regions.Add(new Region(-t, 0f, 1, body)); // the outline line band
			}
			if (stroke && st > 0f)
			{
				AddStroke(regions, 0f, st, strokeAlign, +1, strokeCols); // outer edge (Filled: the only edge; Outline: outer)
				if (!hasFill)
				{
					AddStroke(regions, -t, st, strokeAlign, -1, strokeCols); // inner edge of the line — a true two-sided stroke
				}
			}

			// Collapse the regions into contiguous same-colour segments (inner -> outer); overlaps resolve by priority.
			List<Seg> segments = BuildSegments(regions, hasFill, body);
			if (segments.Count == 0)
			{
				return;
			}

			// Clamp the AA half-width so a band thinner than the fringe can't invert (fringe crossing the solid core).
			float aa = Mathf.Max(antiAlias, 0f) * 0.5f;
			for (int j = 0; j < segments.Count; j++)
			{
				if (hasFill && j == 0) { continue; } // fan segment reaches the centre; its length isn't a constraint
				aa = Mathf.Min(aa, (segments[j].hi - segments[j].lo) * 0.49f);
			}

			// Each boundary becomes a ±aa transition ring-pair: a colour blend between two solid regions, or an alpha fade
			// against the background (outer edge / inner hole). Solid spans fall out as same-colour quads between pairs.
			List<Ring> rings = new();
			int n = segments.Count;
			if (!hasFill)
			{
				rings.Add(new Ring(segments[0].lo - aa, segments[0].cols, 0f)); // inner hole: transparent ...
				rings.Add(new Ring(segments[0].lo + aa, segments[0].cols, 1f)); // ... to solid
			}
			for (int j = 0; j < n; j++)
			{
				float b = segments[j].hi;
				if (j < n - 1)
				{
					rings.Add(new Ring(b - aa, segments[j].cols, 1f));     // colour blend: inner region ...
					rings.Add(new Ring(b + aa, segments[j + 1].cols, 1f)); // ... to outer region (both opaque, no bleed)
				}
				else
				{
					rings.Add(new Ring(b - aa, segments[j].cols, 1f)); // outer edge: solid ...
					rings.Add(new Ring(b + aa, segments[j].cols, 0f)); // ... to transparent
				}
			}

			// Emit vertices: optional fan centre, then every ring (8 verts each).
			int centre = -1;
			if (hasFill)
			{
				centre = vh.currentVertCount;
				vh.AddVert(Vector3.zero, color * centerColor, Vector2.zero);
			}
			int[] ringStart = new int[rings.Count];
			for (int r = 0; r < rings.Count; r++)
			{
				ringStart[r] = vh.currentVertCount;
				Ring ring = rings[r];
				for (int i = 0; i < 8; i++)
				{
					Vector3 pos = corners[i] + miter[i] * (ring.d * invCos[i]);
					Color c = color * ring.cols[i];
					c.a *= ring.alpha;
					vh.AddVert(pos, c, Vector2.zero);
				}
			}

			if (hasFill)
			{
				for (int i = 0; i < 8; i++)
				{
					vh.AddTriangle(centre, ringStart[0] + i, ringStart[0] + (i + 1) % 8);
				}
			}
			for (int r = 0; r < rings.Count - 1; r++)
			{
				int a = ringStart[r];
				int b = ringStart[r + 1];
				for (int i = 0; i < 8; i++)
				{
					int next = (i + 1) % 8;
					Quad(vh, a + i, b + i, b + next, a + next);
				}
			}
		}

		/// <summary>Adds a stroke band for the edge at perpendicular offset <paramref name="edge"/>. <paramref name="outward"/>
		/// is the sign of "away from the body material" at that edge (+1 for the path/outer edge, -1 for the line's inner edge).</summary>
		private static void AddStroke(List<Region> regions, float edge, float st, StrokeAlign align, int outward, Color[] cols)
		{
			float a, b;
			switch (align)
			{
				case StrokeAlign.Outside: a = edge; b = edge + outward * st; break;
				case StrokeAlign.Inside: a = edge; b = edge - outward * st; break;
				default: a = edge - st * 0.5f; b = edge + st * 0.5f; break; // Center
			}
			regions.Add(new Region(Mathf.Min(a, b), Mathf.Max(a, b), 2, cols));
		}

		/// <summary>Sweeps the region endpoints into contiguous covered segments (inner -> outer). For a filled shape the first
		/// segment is the fan (centre -> innermost endpoint). The topmost-priority region wins wherever regions overlap.</summary>
		private static List<Seg> BuildSegments(List<Region> regions, bool hasFill, Color[] fillCols)
		{
			List<float> points = new() { 0f };
			foreach (Region r in regions) { points.Add(r.lo); points.Add(r.hi); }
			points.Sort();

			List<float> ends = new();
			foreach (float p in points)
			{
				if (ends.Count == 0 || Mathf.Abs(p - ends[ends.Count - 1]) > 1e-5f) { ends.Add(p); }
			}

			float dOuter = hasFill ? 0f : float.MinValue;
			foreach (Region r in regions) { dOuter = Mathf.Max(dOuter, r.hi); }

			List<Seg> segs = new();
			if (hasFill)
			{
				segs.Add(new Seg(ends[0] - 1f, ends[0], fillCols)); // fan: centre (sentinel lo) -> first endpoint
			}
			for (int k = 0; k < ends.Count - 1; k++)
			{
				float lo = ends[k], hi = ends[k + 1];
				if (hi > dOuter + 1e-5f) { break; }
				Color[] col = ColorAt(regions, hasFill, fillCols, (lo + hi) * 0.5f);
				if (col == null) { continue; }
				if (segs.Count > 0 && ReferenceEquals(segs[segs.Count - 1].cols, col) && Mathf.Abs(segs[segs.Count - 1].hi - lo) < 1e-4f)
				{
					Seg prev = segs[segs.Count - 1]; prev.hi = hi; segs[segs.Count - 1] = prev; // extend contiguous same-colour run
				}
				else
				{
					segs.Add(new Seg(lo, hi, col));
				}
			}
			return segs;
		}

		/// <summary>Highest-priority region colour covering offset <paramref name="d"/>, or the fill colour inside the path, else null.</summary>
		private static Color[] ColorAt(List<Region> regions, bool hasFill, Color[] fillCols, float d)
		{
			Color[] best = null;
			int bestPriority = int.MinValue;
			foreach (Region r in regions)
			{
				if (d > r.lo + 1e-6f && d < r.hi - 1e-6f && r.priority > bestPriority)
				{
					best = r.cols;
					bestPriority = r.priority;
				}
			}
			if (best != null) { return best; }
			return hasFill && d < 0f ? fillCols : null;
		}

		private readonly struct Region
		{
			public readonly float lo, hi;
			public readonly int priority;
			public readonly Color[] cols;
			public Region(float lo, float hi, int priority, Color[] cols) { this.lo = lo; this.hi = hi; this.priority = priority; this.cols = cols; }
		}

		private struct Seg
		{
			public float lo, hi;
			public Color[] cols;
			public Seg(float lo, float hi, Color[] cols) { this.lo = lo; this.hi = hi; this.cols = cols; }
		}

		private readonly struct Ring
		{
			public readonly float d;
			public readonly Color[] cols;
			public readonly float alpha;
			public Ring(float d, Color[] cols, float alpha) { this.d = d; this.cols = cols; this.alpha = alpha; }
		}

		/// <summary>Outward unit normal of the edge a->b (oriented away from the center at the origin). Zero for a degenerate edge.</summary>
		private static Vector3 EdgeNormal(Vector3 a, Vector3 b)
		{
			Vector3 d = b - a;
			Vector3 n = new Vector3(d.y, -d.x, 0f); // perpendicular to the edge
			if (n.sqrMagnitude < 1e-8f)
			{
				return Vector3.zero;
			}
			n.Normalize();
			// Orient outward: the corners are centered on the origin, so flip toward the edge midpoint's side.
			return Vector3.Dot(n, (a + b) * 0.5f) < 0f ? -n : n;
		}

		private static void Quad(VertexHelper vh, int a, int b, int c, int d)
		{
			vh.AddTriangle(a, b, c);
			vh.AddTriangle(a, c, d);
		}

#if UNITY_EDITOR
		protected override void OnValidate()
		{
			base.OnValidate();
			if (cornerColors == null || cornerColors.Length != 8)
			{
				System.Array.Resize(ref cornerColors, 8);
			}
			SetVerticesDirty();
		}
#endif
	}
}
