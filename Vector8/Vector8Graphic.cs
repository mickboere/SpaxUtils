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

			Vector3[] miter = new Vector3[8];
			float[] invCos = new float[8];
			Vector8Mesh.ComputeMiters(corners, miter, invCos);

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

			// Body/stroke as perpendicular-offset bands (path = 0, + = outward). Fill isn't a band — it's the central fan
			// colouring everything inside the path, which BuildSegments folds in as the first segment.
			List<Vector8Mesh.Region> regions = new();
			if (!hasFill)
			{
				regions.Add(new Vector8Mesh.Region(-t, 0f, 1, body)); // the outline line band
			}
			if (stroke && st > 0f)
			{
				Vector8Mesh.AddStroke(regions, 0f, st, strokeAlign, +1, strokeCols); // outer edge (Filled: the only edge)
				if (!hasFill)
				{
					Vector8Mesh.AddStroke(regions, -t, st, strokeAlign, -1, strokeCols); // inner edge — a true two-sided stroke
				}
			}

			List<Vector8Mesh.Seg> segments = Vector8Mesh.BuildSegments(regions, hasFill, body);
			if (segments.Count == 0)
			{
				return;
			}

			float aa = Vector8Mesh.ClampAA(segments, hasFill, Mathf.Max(antiAlias, 0f) * 0.5f);
			List<Vector8Mesh.Stop> stops = Vector8Mesh.BuildStops(segments, hasFill, aa);

			int centre = -1;
			if (hasFill)
			{
				centre = vh.currentVertCount;
				vh.AddVert(Vector3.zero, color * centerColor, Vector2.zero);
			}

			int firstRing = Vector8Mesh.AppendStrip(vh, corners, miter, invCos, true, stops, color);

			if (hasFill)
			{
				for (int i = 0; i < 8; i++)
				{
					vh.AddTriangle(centre, firstRing + i, firstRing + (i + 1) % 8);
				}
			}
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
