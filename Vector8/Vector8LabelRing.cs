using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Labels the 8 corners of a <see cref="Vector8Graphic"/> with text, from a single pooled template.
	/// Positions come from the graphic itself so the labels can't drift from the mesh. Labels only ever move
	/// along their own spoke, keeping which-corner-is-this unambiguous; see <see cref="Place"/> for the de-clutter.
	/// </summary>
	public class Vector8LabelRing : MonoBehaviour
	{
		[SerializeField, Tooltip("Graphic supplying the corner geometry. Overlay this ring on it — labels are placed in that rect's local space.")]
		private Vector8Graphic source;
		[SerializeField, Tooltip("Instanced 8x, one per corner. Disabled on this object after pooling.")]
		private TMP_Text template;
		[Tooltip("Signed distance from the corner along its spoke: negative sits inside the shape, positive outside, 0 centres the label on the corner.")]
		[SerializeField] private float radialOffset = -8f;
		[Tooltip("Shortest spoke a label may sit on, as a fraction of full extent. Stops low channels piling up in the centre.")]
		[SerializeField, Range(0f, 1f)] private float minRadius = 0.25f;
		[Tooltip("Padding added around a label's text bounds when testing for overlap.")]
		[SerializeField] private Vector2 padding = new Vector2(4f, 2f);
		[Tooltip("How far outwards a label may be pushed to escape an overlap before it gives up.")]
		[SerializeField] private float maxPush = 32f;
		[Tooltip("Optional ring placed before this one; its labels hold their ground and this ring's move aside.")]
		[SerializeField] private Vector8LabelRing avoid;

		[Header("Color")]
		[Tooltip("Tint each label with its corner's colour from the source graphic, instead of the template's own colour.")]
		[SerializeField] private bool copyCornerColors;
		[Tooltip("Shifts the copied colour towards black (-1) or white (+1). 0 leaves it as-is. Alpha is untouched.")]
		[SerializeField, Range(-1f, 1f), Conditional(nameof(copyCornerColors))] private float shade;

		/// <summary>Rects of the currently visible labels, in the source graphic's local space.</summary>
		public IReadOnlyList<Rect> PlacedRects => placedRects;

		private List<TMP_Text> labels = new List<TMP_Text>();
		private List<RectTransform> transforms = new List<RectTransform>();
		private List<Rect> placedRects = new List<Rect>();

		private float[] radii = new float[8];
		private Vector2[] sizes = new Vector2[8];
		private Vector2[] pivots = new Vector2[8];
		private Vector3[] directions = new Vector3[8];
		private bool[] active = new bool[8];
		private float templateAlpha = 1f;

		/// <summary>
		/// Places a label on each corner of <paramref name="radii"/>. Corners whose <paramref name="label"/> returns
		/// null or empty are hidden, so a ring can cover only the channels it has something to say about.
		/// </summary>
		public void Place(Vector8 radii, Func<int, string> label)
		{
			Pool();
			if (source == null || labels.Count < 8)
			{
				return;
			}

			Vector3[] corners = source.GetCornerPositions(radii);
			Vector3[] extents = source.GetCornerExtents();
			Color[] colors = copyCornerColors ? source.GetCornerColors() : null;

			for (int i = 0; i < 8; i++)
			{
				string text = label(i);
				active[i] = !string.IsNullOrEmpty(text);
				labels[i].gameObject.SetActive(active[i]);
				if (!active[i])
				{
					continue;
				}

				labels[i].text = text;
				directions[i] = extents[i].normalized;

				if (colors != null)
				{
					labels[i].color = Shade(colors[i]);
				}

				// Anchor sits on the corner, floored so a near-zero channel still reads outside the hub.
				this.radii[i] = Mathf.Max(corners[i].magnitude, extents[i].magnitude * minRadius) + radialOffset;

				Vector2 preferred = labels[i].GetPreferredValues(text);
				sizes[i] = preferred + padding * 2f;

				// Pivot faces the label away from the shape (or centres it when the offset is zero), replacing
				// a hardcoded pivot table and staying correct when the rect isn't square.
				float sign = Mathf.Approximately(radialOffset, 0f) ? 0f : Mathf.Sign(radialOffset);
				pivots[i] = new Vector2(0.5f - directions[i].x * 0.5f * sign, 0.5f - directions[i].y * 0.5f * sign);
			}

			Declutter();
			Apply();
		}

		/// <summary>
		/// Resolves overlaps by pushing labels outwards along their own spoke — first away from the <see cref="avoid"/>
		/// ring, then away from each other. Pure function of the values, so there's no frame-to-frame wobble.
		/// </summary>
		private void Declutter()
		{
			if (avoid != null)
			{
				IReadOnlyList<Rect> others = avoid.PlacedRects;
				for (int i = 0; i < 8; i++)
				{
					if (!active[i])
					{
						continue;
					}

					for (int o = 0; o < others.Count; o++)
					{
						Separate(i, others[o]);
					}
				}
			}

			// Only adjacent corners can realistically collide; the outer of the pair yields so the inner
			// label keeps the tighter read on its vertex. Two passes settle any knock-on overlap.
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = 0; i < 8; i++)
				{
					int j = (i + 1) % 8;
					if (!active[i] || !active[j])
					{
						continue;
					}

					int outer = radii[i] >= radii[j] ? i : j;
					int inner = outer == i ? j : i;
					Separate(outer, GetRect(inner));
				}
			}
		}

		/// <summary>Pushes label <paramref name="i"/> outwards along its spoke until it clears <paramref name="other"/>.</summary>
		private void Separate(int i, Rect other)
		{
			Rect rect = GetRect(i);
			if (!rect.Overlaps(other))
			{
				return;
			}

			// Distance to travel along the spoke to clear each axis; the cheaper axis wins.
			float overlapX = Mathf.Min(rect.xMax, other.xMax) - Mathf.Max(rect.xMin, other.xMin);
			float overlapY = Mathf.Min(rect.yMax, other.yMax) - Mathf.Max(rect.yMin, other.yMin);
			float dirX = Mathf.Abs(directions[i].x);
			float dirY = Mathf.Abs(directions[i].y);

			float pushX = dirX > 0.001f ? overlapX / dirX : float.MaxValue;
			float pushY = dirY > 0.001f ? overlapY / dirY : float.MaxValue;
			float push = Mathf.Min(pushX, pushY);

			if (push < float.MaxValue)
			{
				radii[i] += Mathf.Min(push, maxPush);
			}
		}

		/// <summary>
		/// Pulls <paramref name="color"/> towards black or white by <see cref="shade"/>. Keeps the template's alpha,
		/// so a semi-transparent fill colour can't drag the text's legibility down with it.
		/// </summary>
		private Color Shade(Color color)
		{
			Color target = shade >= 0f ? Color.white : Color.black;
			Color shaded = Color.Lerp(color, target, Mathf.Abs(shade));
			shaded.a = templateAlpha;
			return shaded;
		}

		private Rect GetRect(int i)
		{
			Vector2 anchor = directions[i] * radii[i];
			Vector2 min = anchor - new Vector2(pivots[i].x * sizes[i].x, pivots[i].y * sizes[i].y);
			return new Rect(min, sizes[i]);
		}

		private void Apply()
		{
			placedRects.Clear();
			for (int i = 0; i < 8; i++)
			{
				if (!active[i])
				{
					continue;
				}

				transforms[i].pivot = pivots[i];
				transforms[i].localPosition = directions[i] * radii[i];
				placedRects.Add(GetRect(i));
			}
		}

		private void Pool()
		{
			if (template == null || labels.Count >= 8)
			{
				return;
			}

			templateAlpha = template.color.a;
			template.gameObject.SetActive(true);
			for (int i = 0; i < 8; i++)
			{
				TMP_Text label = Instantiate(template, transform);
				labels.Add(label);
				transforms.Add(label.rectTransform);
			}
			template.gameObject.SetActive(false);
		}
	}
}
