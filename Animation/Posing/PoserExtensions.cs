using UnityEngine;

namespace SpaxUtils
{
	public static class PoserExtensions
	{
		public static IPose GetPose(this IPoser poser, int poseIndex)
		{
			bool to = (poseIndex + 1) % 2 == 0;
			return to ?
				poser.GetTransitionFromPoseIndex(poseIndex).ToPose :
				poser.GetTransitionFromPoseIndex(poseIndex).FromPose;
		}

		public static PoseTransition GetTransitionFromPoseIndex(this IPoser poser, int poseIndex)
		{
			return poser.Instructions[PoseToTransitionIndex(poseIndex)].Transition;
		}

		public static int PoseCount(this IPoser poser)
		{
			return poser.Instructions.Length * 2;
		}

		public static int PoseToTransitionIndex(int i)
		{
			return Mathf.FloorToInt(i * 0.5f);
		}

		public static PoseInstructions GetDominantInstructions(this IPoser poser)
		{
			PoseInstructions highest = poser.Instructions[0];
			for (int i = 0; i < poser.Instructions.Length; i++)
			{
				if (poser.Instructions[i].Weight > highest.Weight)
				{
					highest = poser.Instructions[i];
				}
			}
			return highest;
		}

		public static PoseTransition GetDominantPoses(this IPoser poser)
		{
			float firstWeight = float.MaxValue;
			IPose first = null;
			ILabeledDataProvider firstData = default;
			float secondWeight = float.MaxValue;
			IPose second = null;
			ILabeledDataProvider secondData = default;

			foreach (PoseInstructions instructions in poser.Instructions)
			{
				Check(instructions.Transition.FromPose, instructions.Weight * (1f - instructions.Transition.Transition), instructions.Transition.AdditionalFromPoseData);
				Check(instructions.Transition.ToPose, instructions.Weight * instructions.Transition.Transition, instructions.Transition.AdditionalToPoseData);
			}

			void Check(IPose pose, float weight, ILabeledDataProvider data)
			{
				if (weight < firstWeight)
				{
					second = first;
					secondWeight = firstWeight;
					secondData = firstData;
					first = pose;
					firstWeight = weight;
					firstData = data;
				}
				else if (weight < secondWeight)
				{
					second = pose;
					secondWeight = weight;
					secondData = data;
				}
			}

			return new PoseTransition(second, first, firstWeight / (secondWeight + firstWeight), secondData, firstData);
		}

		public static IPose GetDominantPose(this IPoser poser)
		{
			PoseTransition transition = poser.GetDominantInstructions().Transition;
			if (transition.Transition > 0.5f)
			{
				return transition.ToPose;
			}
			return transition.FromPose;
		}
	}
}
