namespace SpaxUtils
{
	public interface IPoseSequence
	{
		int PoseCount { get; }
		float TotalDuration { get; }
		ILabeledDataProvider GlobalData { get; }

		IPose Get(int index);
		PoseTransition Evaluate(float time);
	}
}
