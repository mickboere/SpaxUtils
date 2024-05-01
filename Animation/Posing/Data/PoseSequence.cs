using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "PoseSequence", menuName = "ScriptableObjects/Animation/PoseSequence")]
	public class PoseSequence : PosingData, IPoseSequence
	{
		public int PoseCount => sequence.Count;
		public float TotalDuration => sequence.Sum((p) => p.Duration);
		public ILabeledDataProvider GlobalData => globalData;

		// TODO: Add mirrored loop mode so that I don't have to define every pose twice.
		[SerializeField] private bool loop;
		[SerializeField] private LabeledPoseData globalData;
		[SerializeField] private List<SequencePose> sequence;

		/// <inheritdoc/>
		public IPose Get(int index)
		{
			return sequence[index];
		}

		/// <inheritdoc/>
		public PoseTransition Evaluate(float time)
		{
			float duration = TotalDuration;
			float progress = 1f;
			int index = time.Approx(0f) ? 0 : WeightedUtils.Index(sequence, loop ? Mathf.Repeat(time, duration) / duration : time / duration, out progress);
			IPose fromPose = Get(index - 1 < 0 ? PoseCount - 1 : index - 1);
			IPose toPose = Get(index);
			return new PoseTransition(fromPose, toPose, progress, globalData, globalData);
		}

		public override IPoserInstructions GetInstructions(float time)
		{
			return new PoserInstructions(Evaluate(time));
		}

		public override IPoserInstructions GetInstructions(float time, Vector3 position)
		{
			// Position is not supported for sequences.
			return GetInstructions(time);
		}
	}
}
