using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaxUtils.UI
{
	/// <summary>
	/// Scales this rect on X and Y independently so it exactly fills its parent, keeping its authored size.
	/// Assumes it is centred in the parent; only X and Y scale are driven.
	/// </summary>
	[ExecuteAlways, RequireComponent(typeof(RectTransform))]
	public class UIScaledFit : UIBehaviour
	{
		private RectTransform rectTransform;
		private DrivenRectTransformTracker tracker;

		protected override void OnEnable()
		{
			base.OnEnable();
			rectTransform = (RectTransform)transform;
			tracker.Clear();
			tracker.Add(this, rectTransform, DrivenTransformProperties.ScaleX | DrivenTransformProperties.ScaleY);
			// Catches viewport resizes every frame, edit mode included.
			Canvas.preWillRenderCanvases -= Fit;
			Canvas.preWillRenderCanvases += Fit;
			Fit();
		}

		protected override void OnDisable()
		{
			Canvas.preWillRenderCanvases -= Fit;
			tracker.Clear();
			base.OnDisable();
		}

		protected override void OnRectTransformDimensionsChange()
		{
			// Own size changed, e.g. by an AspectRatioFitter during layout.
			Fit();
		}

		private void Fit()
		{
			if (rectTransform == null || !(rectTransform.parent is RectTransform parent))
			{
				return;
			}

			Vector2 size = rectTransform.rect.size;
			Vector2 area = parent.rect.size;
			if (size.x <= 0f || size.y <= 0f)
			{
				return;
			}

			Vector3 scale = new Vector3(area.x / size.x, area.y / size.y, rectTransform.localScale.z);
			if (scale != rectTransform.localScale)
			{
				rectTransform.localScale = scale;
			}
		}
	}
}
