using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Renders the static background reference "web" for a <see cref="Vector8"/> visualizer: concentric octagon rings and
	/// optional radial spokes, drawn directly into the UI mesh. Data-independent; place it behind a <see cref="Vector8Graphic"/>.
	/// Each spoke fades from the hub colour to its own corner colour (rings interpolate between them and fade inward), and
	/// every line supports a <see cref="stroke"/> border and an anti-alias fringe — geometry via <see cref="Vector8Mesh"/>.
	/// </summary>
	[ExecuteInEditMode]
	public class Vector8GridGraphic : MaskableGraphic
	{
		[Header("Layout")]
		[SerializeField] private float scale = 1f;
		[Tooltip("Width in pixels of the transparent fade fringe straddling each line. 0 = hard edges.")]
		[SerializeField] private float antiAlias = 1f;

		[Header("Rings")]
		[SerializeField, Min(0)] private int rings = 4;
		[SerializeField] private float ringThickness = 1.5f;

		[Header("Spokes")]
		[SerializeField] private bool spokes = true;
		[SerializeField, Conditional(nameof(spokes))] private float spokeThickness = 1.5f;

		[Header("Color")]
		[Tooltip("When off, the whole web uses the flat 'Body Color'. When on, each spoke takes its own corner colour and the rings interpolate between them.")]
		[SerializeField] private bool useCornerColors = true;
		[Tooltip("Flat colour for the whole web, used when Corner Colors is off. Kept separate from the graphic's own 'Color' so that tint can stay neutral and not bleed into the stroke.")]
		[SerializeField, Conditional(nameof(useCornerColors), true)] private Color bodyColor = Color.white;
		[Tooltip("Per-corner colors (N, NE, E, SE, S, SW, W, NW). Each spoke fades from Center Color at the hub to its corner colour at the tip; rings interpolate around the loop AND fade toward the centre as they get smaller.")]
		[SerializeField, Conditional(nameof(useCornerColors))]
		private Color[] cornerColors = new Color[8]
		{
			Color.white, Color.white, Color.white, Color.white,
			Color.white, Color.white, Color.white, Color.white
		};
		[Tooltip("Colour at the hub. Spokes start here, and inner rings fade toward it.")]
		[SerializeField, Conditional(nameof(useCornerColors))] private Color centerColor = Color.white;

		[Header("Stroke")]
		[Tooltip("Draws a border along BOTH sides of every ring and spoke. All strokes are laid down BEHIND all lines, so one line's border never covers another line.")]
		[SerializeField] private bool stroke;
		[Tooltip("Border width, in the same units as the ring/spoke thickness.")]
		[SerializeField, Conditional(nameof(stroke))] private float strokeThickness = 1f;
		[Tooltip("Where the border sits relative to the line's edges: Outside (line keeps its width, border sits beyond it), Center, or Inside (border eats into the line's width).")]
		[SerializeField, Conditional(nameof(stroke))] private StrokeAlign strokeAlign = StrokeAlign.Outside;
		[Tooltip("Base the border on the line's own colours instead of a flat colour. Stroke Color still tints it either way.")]
		[SerializeField, Conditional(nameof(stroke))] private bool strokeUseBodyColors;
		[Tooltip("Border colour. Tints the line colours when Stroke Use Body Colors is on; otherwise it IS the flat border colour.")]
		[SerializeField, Conditional(nameof(stroke))] private Color strokeColor = Color.black;

		protected void Update()
		{
			if (!Application.isPlaying)
			{
				SetVerticesDirty();
			}
		}

		protected override void OnPopulateMesh(VertexHelper vh)
		{
			vh.Clear();

			// Outer octagon corners (value 1 -> rect edge), shared by rings and spokes.
			Vector3[] outer = Vector8.One.GetPositions3DRect(scale, rectTransform.rect);

			// A ring is the outer octagon scaled about the origin, so its miters/cos are identical at every radius.
			Vector3[] miter = new Vector3[8];
			float[] invCos = new float[8];
			Vector8Mesh.ComputeMiters(outer, miter, invCos);

			Color[] tips = new Color[8];
			for (int i = 0; i < 8; i++)
			{
				tips[i] = useCornerColors ? cornerColors[i] : bodyColor;
			}
			Color hub = useCornerColors ? centerColor : bodyColor;

			// How the stroke splits around a line's edge: it grows outward by `expand` and eats inward by `inset`.
			float st = stroke ? Mathf.Max(strokeThickness, 0f) : 0f;
			float expand, inset;
			switch (strokeAlign)
			{
				case StrokeAlign.Inside: expand = 0f; inset = st; break;
				case StrokeAlign.Center: expand = st * 0.5f; inset = st * 0.5f; break;
				default: expand = st; inset = 0f; break; // Outside
			}

			Vector3[] spokePoints = new Vector3[2];
			Vector3[] spokeNormals = new Vector3[2];
			float[] straight = { 1f, 1f }; // a spoke has no corner, so no miter correction
			Vector3[] ringPoints = new Vector3[8];
			Color[] spokeColors = new Color[2];
			Color[] ringColors = new Color[8];
			Color[] drawColors2 = new Color[2];
			Color[] drawColors8 = new Color[8];

			// TWO PASSES. Every stroke is laid down first, so no line's border can ever cover another line. Each stroke is a
			// solid slab that also runs UNDER its own body, so the body's AA fringe fades onto opaque stroke rather than onto
			// the background — which is what stops a translucent seam appearing where the two meet.
			for (int pass = 0; pass < 2; pass++)
			{
				bool strokePass = pass == 0;
				if (strokePass && st <= 0f)
				{
					continue;
				}

				if (spokes && spokeThickness > 0f)
				{
					Span(strokePass, spokeThickness * 0.5f, expand, inset, out float lo, out float hi);
					for (int i = 0; i < 8; i++)
					{
						Vector3 dir = outer[i].sqrMagnitude > 1e-6f ? outer[i].normalized : Vector3.right;
						spokeNormals[0] = spokeNormals[1] = new Vector3(-dir.y, dir.x, 0f);
						spokePoints[0] = Vector3.zero;
						spokePoints[1] = outer[i];
						spokeColors[0] = hub;
						spokeColors[1] = tips[i];

						Resolve(strokePass, spokeColors, drawColors2);
						AddBand(vh, spokePoints, spokeNormals, straight, false, lo, hi, drawColors2);
					}
				}

				if (ringThickness > 0f)
				{
					Span(strokePass, ringThickness * 0.5f, expand, inset, out float lo, out float hi);
					for (int k = 1; k <= rings; k++)
					{
						float f = (float)k / rings;
						for (int i = 0; i < 8; i++)
						{
							ringPoints[i] = outer[i] * f;
							// Inner rings sit closer to the hub, so fade them toward the centre colour — matching the spokes.
							ringColors[i] = Color.Lerp(hub, tips[i], f);
						}

						Resolve(strokePass, ringColors, drawColors8);
						AddBand(vh, ringPoints, miter, invCos, true, lo, hi, drawColors8);
					}
				}
			}
		}

		/// <summary>The perpendicular span a pass covers: the stroke slab (line + its border), or the line body inset by the border.</summary>
		private static void Span(bool strokePass, float half, float expand, float inset, out float lo, out float hi)
		{
			if (strokePass)
			{
				lo = -half - expand;
				hi = half + expand;
			}
			else
			{
				float bite = Mathf.Min(inset, half * 0.99f); // an inside stroke must never invert the body
				lo = -half + bite;
				hi = half - bite;
			}
		}

		private void Resolve(bool strokePass, Color[] lineColors, Color[] result)
		{
			for (int i = 0; i < lineColors.Length; i++)
			{
				result[i] = !strokePass ? lineColors[i]
					: strokeUseBodyColors ? lineColors[i] * strokeColor
					: strokeColor;
			}
		}

		/// <summary>Emits one band of perpendicular span [lo,hi] along the path, with an AA fringe straddling both edges.</summary>
		private void AddBand(VertexHelper vh, Vector3[] points, Vector3[] normals, float[] invCos, bool closed, float lo, float hi, Color[] colors)
		{
			if (hi - lo <= 0f)
			{
				return;
			}

			List<Vector8Mesh.Region> regions = new() { new Vector8Mesh.Region(lo, hi, 1, colors) };
			List<Vector8Mesh.Seg> segments = Vector8Mesh.BuildSegments(regions, false, null);
			if (segments.Count == 0)
			{
				return;
			}

			float aa = Vector8Mesh.ClampAA(segments, false, Mathf.Max(antiAlias, 0f) * 0.5f);
			List<Vector8Mesh.Stop> stops = Vector8Mesh.BuildStops(segments, false, aa);
			Vector8Mesh.AppendStrip(vh, points, normals, invCos, closed, stops, color);
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
