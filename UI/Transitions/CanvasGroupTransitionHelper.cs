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

		//public override void Fill(Action callback = null, float overrideTime = -1)
		//{
		//	// Activate gameObject to allow its update loop to run in case needed.
		//	canvasGroup.gameObject.SetActive(true);

		//	base.Fill(callback, overrideTime);
		//}

		protected override void OnProgressed()
		{
			canvasGroup.alpha = Evaluation;
			canvasGroup.interactable = IsFull;
			canvasGroup.blocksRaycasts = IsFull;
			canvasGroup.gameObject.SetActive(!IsEmpty);

			base.OnProgressed();
		}
	}
}
