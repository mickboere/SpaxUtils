using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class TransitionSettings
	{
		private const string TT_RD = "0 = no delay / crossfade, 1 = full delay / no crossfade";

		public bool Realtime => realtime;
		public float RelativeDelay => relativeDelay;
		public float InTime => inTime;
		public float OutTime => outTime;
		public AnimationCurve Intro => intro;
		public AnimationCurve Outro => outro;

		[SerializeField] private bool realtime;
		[SerializeField, Range(0f, 1f), Tooltip(TT_RD)] private float relativeDelay;
		[SerializeField] private float inTime = 0.5f;
		[SerializeField] private float outTime = 0.3f;
		[SerializeField] private AnimationCurve intro = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField] private AnimationCurve outro = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
	}
}
