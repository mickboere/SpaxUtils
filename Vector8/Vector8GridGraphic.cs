using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils
{
	/// <summary>
	/// Renders the static background reference "web" for a <see cref="Vector8"/> visualizer:
	/// concentric octagon rings and optional radial spokes, drawn directly into the UI mesh.
	/// Data-independent; place it behind a <see cref="Vector8Graphic"/> and give it a faint color/alpha.
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

			if (spokes && spokeThickness > 0f)
			{
				for (int i = 0; i < 8; i++)
				{
					AddLine(vh, Vector3.zero, outer[i], spokeThickness * 0.5f);
				}
			}

			for (int k = 1; k <= rings; k++)
			{
				AddRing(vh, (float)k / rings, outer, ringThickness * 0.5f);
			}
		}

		/// <summary>Adds an octagon outline at radius fraction <paramref name="f"/> of <paramref name="outer"/>.</summary>
		private void AddRing(VertexHelper vh, float f, Vector3[] outer, float halfWidth)
		{
			float halfAA = antiAlias * 0.5f;
			bool aa = antiAlias > 0f;
			int start = vh.currentVertCount;

			for (int i = 0; i < 8; i++)
			{
				Vector3 p = outer[i] * f;
				Vector3 radial = Radial(p);
				Color32 c = color;
				Color32 t = c;
				t.a = 0;

				vh.AddVert(p - radial * (halfWidth + halfAA), t, Vector2.zero); // +0 inner fringe
				vh.AddVert(p - radial * (halfWidth - halfAA), c, Vector2.zero); // +1 inner solid
				vh.AddVert(p + radial * (halfWidth - halfAA), c, Vector2.zero); // +2 outer solid
				vh.AddVert(p + radial * (halfWidth + halfAA), t, Vector2.zero); // +3 outer fringe
			}
			for (int i = 0; i < 8; i++)
			{
				int a = start + i * 4;
				int b = start + (i + 1) % 8 * 4;
				Quad(vh, a + 1, a + 2, b + 2, b + 1); // main band
				if (aa)
				{
					Quad(vh, a + 0, a + 1, b + 1, b + 0); // inner fringe
					Quad(vh, a + 2, a + 3, b + 3, b + 2); // outer fringe
				}
			}
		}

		/// <summary>Adds a straight line from <paramref name="p0"/> to <paramref name="p1"/> with a 50/50 AA fringe on both long edges.</summary>
		private void AddLine(VertexHelper vh, Vector3 p0, Vector3 p1, float halfWidth)
		{
			float halfAA = antiAlias * 0.5f;
			bool aa = antiAlias > 0f;
			Vector3 dir = (p1 - p0).normalized;
			Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
			Color32 c = color;
			Color32 t = c;
			t.a = 0;

			int s = vh.currentVertCount;
			// Per end (p0 then p1): [leftFringe, leftSolid, rightSolid, rightFringe].
			AddLineEnd(vh, p0, perp, halfWidth, halfAA, c, t);
			AddLineEnd(vh, p1, perp, halfWidth, halfAA, c, t);

			Quad(vh, s + 1, s + 2, s + 6, s + 5); // main band
			if (aa)
			{
				Quad(vh, s + 0, s + 1, s + 5, s + 4); // left fringe
				Quad(vh, s + 2, s + 3, s + 7, s + 6); // right fringe
			}
		}

		private static void AddLineEnd(VertexHelper vh, Vector3 p, Vector3 perp, float halfWidth, float halfAA, Color32 c, Color32 t)
		{
			vh.AddVert(p + perp * (halfWidth + halfAA), t, Vector2.zero); // left fringe
			vh.AddVert(p + perp * (halfWidth - halfAA), c, Vector2.zero); // left solid
			vh.AddVert(p - perp * (halfWidth - halfAA), c, Vector2.zero); // right solid
			vh.AddVert(p - perp * (halfWidth + halfAA), t, Vector2.zero); // right fringe
		}

		private static Vector3 Radial(Vector3 v)
		{
			return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
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
			SetVerticesDirty();
		}
#endif
	}
}
