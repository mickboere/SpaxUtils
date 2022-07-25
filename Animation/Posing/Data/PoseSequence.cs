using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{

	[CreateAssetMenu(fileName = "PoseSequence", menuName = "ScriptableObjects/Animation/PoseSequence")]
	public class PoseSequence : ScriptableObject, IPoseSequence
	{
		public int PoseCount => sequence.Count;
		public float TotalDuration => sequence.Sum((p) => p.Duration);
		public ILabeledDataProvider GlobalData => globalData;

		[SerializeField] private bool loop;
		[SerializeField] private LabeledPoseData globalData;
		[SerializeField] private List<Pose> sequence;

		/// <summary>
		/// Returns <see cref="Pose"/> at <paramref name="index"/>.
		/// </summary>
		public IPose Get(int index)
		{
			return sequence[index];
		}

		/// <summary>
		/// Samples the pose for <paramref name="time"/>.
		/// </summary>
		/// <param name="time">The amount of time into the total duration of the sequence.</param>
		/// <returns>A <see cref="PoseTransition"/> object containing the target poses and their data.</returns>
		public PoseTransition Evaluate(float time)
		{
			float duration = TotalDuration;
			float progress = 1f;
			int index = time.Approx(0f) ? 0 : WeightedUtils.Index(sequence, loop ? Mathf.Repeat(time, duration) / duration : time / duration, out progress);
			IPose fromPose = Get(index - 1 < 0 ? PoseCount - 1 : index - 1);
			IPose toPose = Get(index);
			return new PoseTransition(fromPose, toPose, progress, globalData, globalData);
		}
	}
}
