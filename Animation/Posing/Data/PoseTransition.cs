using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Contains two <see cref="IPose"/>s and a progress value indicating the progress from A to B.
	/// </summary>
	public struct PoseTransition
	{
		/// <summary>
		/// Evaluates the transition curve of <see cref="ToPose"/> with <see cref="Progress"/>.
		/// </summary>
		public float Transition => ToPose == null ? 0f : ToPose.EvaluateTransition(Progress);

		/// <summary>
		/// Reference to pose A.
		/// </summary>
		public IPose FromPose { get; set; }
		public ILabeledDataProvider AdditionalFromPoseData { get; set; }

		/// <summary>
		/// Reference to pose B.
		/// </summary>
		public IPose ToPose { get; set; }
		public ILabeledDataProvider AdditionalToPoseData { get; set; }

		/// <summary>
		/// Interpolation value from pose A to pose B.
		/// </summary>
		public float Progress;

		public PoseTransition(IPose poseA, IPose poseB, float progress, ILabeledDataProvider additionalDataA = null, ILabeledDataProvider additionalDataB = null)
		{
			FromPose = poseA;
			ToPose = poseB;
			Progress = progress;
			AdditionalFromPoseData = additionalDataA;
			AdditionalToPoseData = additionalDataB;
		}

		public bool TryEvaluateFloat(string identifier, float defaultIfNull, out float result)
		{
			bool aSuccess = FromPose.Data.TryGetFloat(identifier, defaultIfNull, out float a);
			if (!aSuccess)
			{
				aSuccess = AdditionalFromPoseData.TryGetFloat(identifier, defaultIfNull, out a);
			}

			bool bSuccess = ToPose.Data.TryGetFloat(identifier, defaultIfNull, out float b);
			if (!bSuccess)
			{
				AdditionalToPoseData.TryGetFloat(identifier, defaultIfNull, out b);
			}

			result = Mathf.Lerp(a, b, Progress);
			return aSuccess && bSuccess;
		}

		public bool TryEvaluateBool(string identifier, bool defaultIfNull, out bool result)
		{
			bool a = defaultIfNull;
			bool b = defaultIfNull;

			bool aSuccess = FromPose.Data.TryGetBool(identifier, defaultIfNull, out a);
			if (!aSuccess)
			{
				aSuccess = AdditionalFromPoseData.TryGetBool(identifier, defaultIfNull, out a);
			}

			bool bSuccess = ToPose.Data.TryGetBool(identifier, defaultIfNull, out b);
			if (!bSuccess)
			{
				AdditionalToPoseData.TryGetBool(identifier, defaultIfNull, out b);
			}

			result = Progress > 0.5f ? b : a;
			return aSuccess && bSuccess;
		}

		public override string ToString()
		{
			return $"({(FromPose.Clip != null ? FromPose.Clip.name : "NULL")}, {(ToPose.Clip != null ? ToPose.Clip.name : "NULL")}, {Progress})";
		}
	}
}
