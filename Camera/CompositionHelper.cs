using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Draws composition guide overlays in the Game view for framing shots.
	/// Editor-only, strips itself from builds.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(Camera))]
	public class CompositionHelper : MonoBehaviour
	{
#if UNITY_EDITOR
		[Flags]
		public enum Guide
		{
			None = 0,
			RuleOfThirds = 1 << 0,
			CenterCross = 1 << 1,
			Crosshair = 1 << 2,
			GoldenRatio = 1 << 3,
			Diagonals = 1 << 4,
			SafeAreas = 1 << 5,
		}

		[SerializeField] private Guide activeGuides = Guide.RuleOfThirds | Guide.Crosshair;
		[SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);
		[SerializeField] private Color safeAreaColor = new Color(1f, 0.5f, 0f, 0.25f);

		private Texture2D lineTex;

		private void OnDestroy()
		{
			if (lineTex != null)
			{
				DestroyImmediate(lineTex);
			}
		}

		private void OnGUI()
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			if (activeGuides == Guide.None)
			{
				return;
			}

			EnsureTexture();

			float w = Screen.width;
			float h = Screen.height;

			if ((activeGuides & Guide.RuleOfThirds) != 0)
			{
				DrawRuleOfThirds(w, h);
			}

			if ((activeGuides & Guide.CenterCross) != 0)
			{
				DrawCenterCross(w, h);
			}

			if ((activeGuides & Guide.Crosshair) != 0)
			{
				DrawCrosshair(w, h);
			}

			if ((activeGuides & Guide.GoldenRatio) != 0)
			{
				DrawGoldenRatio(w, h);
			}

			if ((activeGuides & Guide.Diagonals) != 0)
			{
				DrawDiagonals(w, h);
			}

			if ((activeGuides & Guide.SafeAreas) != 0)
			{
				DrawSafeAreas(w, h);
			}
		}

		// Rule of Thirds: 3x3 grid dividing the frame into nine equal parts.
		private void DrawRuleOfThirds(float w, float h)
		{
			DrawLine(w / 3f, 0f, 1f, h, lineColor);
			DrawLine(2f * w / 3f, 0f, 1f, h, lineColor);
			DrawLine(0f, h / 3f, w, 1f, lineColor);
			DrawLine(0f, 2f * h / 3f, w, 1f, lineColor);
		}

		// Center Cross: full-width horizontal and vertical lines through the center.
		private void DrawCenterCross(float w, float h)
		{
			DrawLine(w / 2f, 0f, 1f, h, lineColor);
			DrawLine(0f, h / 2f, w, 1f, lineColor);
		}

		// Crosshair: small cross at the exact center.
		private void DrawCrosshair(float w, float h)
		{
			float size = Mathf.Min(w, h) * 0.02f;
			DrawLine(w / 2f, h / 2f - size, 1f, size * 2f, lineColor);
			DrawLine(w / 2f - size, h / 2f, size * 2f, 1f, lineColor);
		}

		// Golden Ratio: guides at ~0.382 and ~0.618 of frame dimensions.
		private void DrawGoldenRatio(float w, float h)
		{
			const float phi = 0.381966f;
			DrawLine(w * phi, 0f, 1f, h, lineColor);
			DrawLine(w * (1f - phi), 0f, 1f, h, lineColor);
			DrawLine(0f, h * phi, w, 1f, lineColor);
			DrawLine(0f, h * (1f - phi), w, 1f, lineColor);
		}

		// Diagonals: corner-to-corner X.
		private void DrawDiagonals(float w, float h)
		{
			DrawAngledLine(0f, 0f, w, h, lineColor);
			DrawAngledLine(w, 0f, 0f, h, lineColor);
		}

		// Safe Areas: action safe (93%) and title safe (80%) rectangles.
		private void DrawSafeAreas(float w, float h)
		{
			// Action safe: 93% of frame.
			DrawSafeRect(w, h, 0.035f, safeAreaColor);
			// Title safe: 80% of frame.
			DrawSafeRect(w, h, 0.1f, safeAreaColor);
		}

		private void DrawSafeRect(float w, float h, float margin, Color color)
		{
			float mx = w * margin;
			float my = h * margin;
			float iw = w - 2f * mx;
			float ih = h - 2f * my;

			// Top edge.
			DrawLine(mx, my, iw, 1f, color);
			// Bottom edge.
			DrawLine(mx, my + ih, iw, 1f, color);
			// Left edge.
			DrawLine(mx, my, 1f, ih, color);
			// Right edge.
			DrawLine(mx + iw, my, 1f, ih, color);
		}

		private void DrawLine(float x, float y, float width, float height, Color color)
		{
			EnsureTexture();
			lineTex.SetPixel(0, 0, color);
			lineTex.Apply();
			GUI.DrawTexture(new Rect(x, y, width, height), lineTex);
		}

		private void DrawAngledLine(float x1, float y1, float x2, float y2, Color color)
		{
			EnsureTexture();
			lineTex.SetPixel(0, 0, color);
			lineTex.Apply();

			Matrix4x4 savedMatrix = GUI.matrix;
			float dx = x2 - x1;
			float dy = y2 - y1;
			float length = Mathf.Sqrt(dx * dx + dy * dy);
			float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

			GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
			GUI.DrawTexture(new Rect(x1, y1 - 0.5f, length, 1f), lineTex);
			GUI.matrix = savedMatrix;
		}

		private void EnsureTexture()
		{
			if (lineTex == null)
			{
				lineTex = new Texture2D(1, 1);
			}
		}
#endif
	}
}
