using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Keeps a graphic's shape true under stretched, sheared or mirrored parents; it still follows their layout.
	/// Works on TMP text (following its baseline) and on regular graphics such as icons.
	/// </summary>
	[RequireComponent(typeof(Graphic))]
	public class UIUndistort : BaseMeshEffect
	{
		private const float EPSILON = 0.0001f;

		private TMP_Text text;
		private Vector4 lastLinear;

		protected override void OnEnable()
		{
			text = GetComponent<TMP_Text>();
			if (text != null)
			{
				text.OnPreRenderText -= OnPreRenderText;
				text.OnPreRenderText += OnPreRenderText;
			}
			lastLinear = GetLinear();
			base.OnEnable();
			Regenerate();
		}

		protected override void OnDisable()
		{
			if (text != null)
			{
				text.OnPreRenderText -= OnPreRenderText;
			}
			base.OnDisable();
			Regenerate();
		}

		protected void LateUpdate()
		{
			// Text and vertices rebuild on their own; only a changed parent transform needs a nudge.
			Vector4 linear = GetLinear();
			if ((linear - lastLinear).sqrMagnitude > EPSILON * EPSILON)
			{
				lastLinear = linear;
				Regenerate();
			}
		}

		public override void ModifyMesh(VertexHelper vh)
		{
			if (!IsActive() || text != null || !TryGetCorrection(out Vector4 c))
			{
				return;
			}

			UIVertex vertex = default;
			for (int i = 0; i < vh.currentVertCount; i++)
			{
				vh.PopulateUIVertex(ref vertex, i);
				vertex.position = Correct(vertex.position, c);
				vh.SetUIVertex(vertex, i);
			}
		}

		private void OnPreRenderText(TMP_TextInfo info)
		{
			if (!IsActive() || !TryGetCorrection(out Vector4 c))
			{
				return;
			}

			for (int m = 0; m < info.materialCount && m < info.meshInfo.Length; m++)
			{
				Vector3[] vertices = info.meshInfo[m].vertices;
				int count = Mathf.Min(info.meshInfo[m].vertexCount, vertices.Length);
				for (int i = 0; i < count; i++)
				{
					vertices[i] = Correct(vertices[i], c);
				}
			}
		}

		private void Regenerate()
		{
			if (text != null)
			{
				text.havePropertiesChanged = true;
			}
			else if (graphic != null)
			{
				graphic.SetVerticesDirty();
			}
		}

		/// <summary>
		/// The 2D linear part (xx, yx, xy, yy) of this graphic's transform relative to its root canvas.
		/// </summary>
		private Vector4 GetLinear()
		{
			Canvas canvas = graphic != null ? graphic.canvas : null;
			Matrix4x4 m = canvas != null ?
				canvas.rootCanvas.transform.worldToLocalMatrix * transform.localToWorldMatrix :
				transform.localToWorldMatrix;
			return new Vector4(m.m00, m.m10, m.m01, m.m11);
		}

		/// <summary>
		/// Local-space map (c00, c01, c10, c11) that turns the distorted linear transform into a rotation and uniform scale.
		/// Keeps the smallest axis scale and the baseline direction, reversed when mirrored so text reads forwards.
		/// </summary>
		private bool TryGetCorrection(out Vector4 c)
		{
			c = new Vector4(1f, 0f, 0f, 1f);
			Vector4 l = GetLinear();
			float a = l.x, cc = l.y, b = l.z, d = l.w; // Columns: x axis (a, cc), y axis (b, d).
			float det = a * d - b * cc;
			if (Mathf.Abs(det) < EPSILON * EPSILON)
			{
				return false;
			}

			// Smallest singular value of the 2x2 map.
			float e = (a + d) * 0.5f;
			float f = (a - d) * 0.5f;
			float g = (cc + b) * 0.5f;
			float h = (cc - b) * 0.5f;
			float scale = Mathf.Abs(Mathf.Sqrt(e * e + h * h) - Mathf.Sqrt(f * f + g * g));

			float flip = det < 0f ? -1f : 1f;
			Vector2 baseline = new Vector2(a * flip, cc * flip).normalized;
			float ux = baseline.x * scale;
			float uy = baseline.y * scale;

			// c = inverse(L) * U, with U = [[ux, -uy], [uy, ux]].
			float inv = 1f / det;
			c = new Vector4(
				inv * (d * ux - b * uy),
				inv * (-d * uy - b * ux),
				inv * (-cc * ux + a * uy),
				inv * (cc * uy + a * ux));

			// Undistorted already, e.g. a plain rotation or uniform scale.
			return Mathf.Abs(c.x - 1f) > EPSILON || Mathf.Abs(c.y) > EPSILON ||
				Mathf.Abs(c.z) > EPSILON || Mathf.Abs(c.w - 1f) > EPSILON;
		}

		private static Vector3 Correct(Vector3 p, Vector4 c)
		{
			return new Vector3(c.x * p.x + c.y * p.y, c.z * p.x + c.w * p.y, p.z);
		}
	}
}
