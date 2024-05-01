namespace SpaxUtils
{
	public interface IPoseSequence
	{
		int PoseCount { get; }
		float TotalDuration { get; }
		ILabeledDataProvider GlobalData { get; }

		/// <summary>
		/// Returns <see cref="SequencePose"/> at <paramref name="index"/>.
		/// </summary>
		IPose Get(int index);

		/// <summary>
		/// Samples the pose for <paramref name="time"/>.
		/// </summary>
		/// <param name="time">The amount of time into the total duration of the sequence.</param>
		/// <returns>A <see cref="PoseTransition"/> object containing the target poses and their data.</returns>
		PoseTransition Evaluate(float time);
	}
}
