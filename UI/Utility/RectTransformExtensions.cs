using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils.UI
{
	public static class RectTransformExtensions
	{
		public static void MoveToScreenPositionAnchored(this RectTransform rectTransform, Camera camera, Vector3 worldPos)
		{
			MoveToScreenPositionAnchored(rectTransform, camera, worldPos, Vector2.zero);
		}

		public static void MoveToScreenPositionAnchored(this RectTransform rectTransform, Camera camera, Vector3 worldPos, Vector2 offset)
		{
			Vector2 targetScreenPos = camera.WorldToViewportPoint(worldPos);
			MoveToScreenPositionAnchored(rectTransform, targetScreenPos, offset);
		}

		public static void MoveToScreenPositionAnchored(this RectTransform rectTransform, Vector2 screenPos)
		{
			MoveToScreenPositionAnchored(rectTransform, screenPos, new Vector2());
		}

		public static void MoveToScreenPositionAnchored(this RectTransform rectTransform, Vector2 screenPos, Vector2 offset)
		{
			rectTransform.anchorMin = screenPos;
			rectTransform.anchorMax = screenPos;
			rectTransform.anchoredPosition = offset;
		}

		public static Rect GetRectOnCanvas(this RectTransform rectTransform, RectTransform canvas, Camera camera)
		{
			Vector3[] corners = new Vector3[4];
			rectTransform.GetWorldCorners(corners);

			// Convert world space to screen space in pixel values and round to integers
			Vector3 size = canvas.rect.size;
			for (int i = 0; i < corners.Length; i++)
			{
				corners[i] = camera.WorldToViewportPoint(corners[i]);
				corners[i] = corners[i].Multiply(size);
				//corners[i] = new Vector3(Mathf.RoundToInt(corners[i].x), Mathf.RoundToInt(corners[i].y), corners[i].z);
			}

			// Calculate the screen space rectangle
			float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
			float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
			float width = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x) - minX;
			float height = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y) - minY;

			return new Rect(minX, minY, width, height);
		}

		/// <summary>
		/// Copies all RectTransform properties (anchors, pivot, position, size, rotation, scale) 
		/// from the source to the target.
		/// </summary>
		public static void CopyFrom(this RectTransform target, RectTransform source)
		{
			if (target == null || source == null)
			{
				return;
			}

			// 1. Anchors & Pivot must be set first, as they define the coordinate space
			target.anchorMin = source.anchorMin;
			target.anchorMax = source.anchorMax;
			target.pivot = source.pivot;

			// 2. Size
			target.sizeDelta = source.sizeDelta;

			// 3. Position
			target.anchoredPosition3D = source.anchoredPosition3D;

			// 4. Rotation & Scale
			target.localRotation = source.localRotation;
			target.localScale = source.localScale;
		}
	}
}
