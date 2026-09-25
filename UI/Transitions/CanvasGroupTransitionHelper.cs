using System;
using UnityEngine;

namespace SpaxUtils.UI
{
	/// <summary>
	/// <see cref="TransitionHelper"/> implementation that handles a <see cref="CanvasGroup"/> transition.
	/// Will deactivate the entire GameObject if group is invisible to save resources.
	/// </summary>
	public class CanvasGroupTransitionHelper : TransitionHelper
	{
		// Visibility at which a filling group starts accepting input.
		private const float INTERACTABLE_VISIBILITY = 0.5f;

		private readonly CanvasGroup canvasGroup;

		public CanvasGroupTransitionHelper(CanvasGroup canvasGroup, bool realtime = true, float relativeDelay = 1f, float inTime = 1f, float outTime = 1f, AnimationCurve intro = null, AnimationCurve outro = null)
			: base(realtime, relativeDelay, inTime, outTime, intro, outro)
		{
			this.canvasGroup = canvasGroup;
		}

		public CanvasGroupTransitionHelper(CanvasGroup canvasGroup, TransitionSettings settings)
			: base(settings)
		{
			this.canvasGroup = canvasGroup;
		}

		public override void Fill(Action callback = null, float delay = 0f, float overrideTime = -1)
		{
			// Activate gameObject to allow its update loop to run in case needed.
			canvasGroup.gameObject.SetActive(true);

			base.Fill(callback, delay, overrideTime);
		}

		protected override void OnProgressed()
		{
			if (canvasGroup != null)
			{
				// Emptying drops input at once, so a closing group can't take a second press.
				bool interactable = IsFull || Control > 0f && Evaluation >= INTERACTABLE_VISIBILITY;
				canvasGroup.alpha = Evaluation;
				canvasGroup.interactable = interactable;
				canvasGroup.blocksRaycasts = interactable;
				canvasGroup.gameObject.SetActive(!IsEmpty);
			}

			base.OnProgressed();
		}
	}
}
