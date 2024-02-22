using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaxUtils
{
	[Serializable]
	public class SequencePose : IPose
	{
		public AnimationClip Clip => clip;
		public bool Mirror => mirror;
		public float Duration => duration;
		public ILabeledDataProvider Data => data;

		public float ElementWeight => Duration;

		[SerializeField] private AnimationClip clip;
		[SerializeField] private bool mirror;
		[SerializeField] private float duration;
		[SerializeField] private AnimationCurve transition;
		[SerializeField] private LabeledPoseData data;

		public SequencePose(AnimationClip clip, bool mirror, float duration, AnimationCurve transition, LabeledPoseData data)
		{
			this.clip = clip;
			this.mirror = mirror;
			this.duration = duration;
			this.transition = transition;
			this.data = data;
		}

		public float EvaluateTransition(float x)
		{
			return transition.Evaluate(x);
		}
	}
}
