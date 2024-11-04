using UnityEngine;

namespace SpaxUtils.UI
{
	public static class UIUtilities
	{
		public static void MoveToScreenPosition(this RectTransform rectTransform, Camera camera, Vector3 worldPos)
		{
			MoveToScreenPosition(rectTransform, camera, worldPos, Vector2.zero);
		}

		public static void MoveToScreenPosition(this RectTransform rectTransform, Camera camera, Vector3 worldPos, Vector2 offset)
		{
			Vector2 targetScreenPos = camera.WorldToViewportPoint(worldPos);
			rectTransform.anchorMin = targetScreenPos;
			rectTransform.anchorMax = targetScreenPos;
			rectTransform.anchoredPosition = offset;
		}
	}
}