namespace SpaxUtils
{
	/// <summary>
	/// Interface containing a pose transition and its effective weight.
	/// </summary>
	public struct PoseInstructions
	{
		public PoseTransition Transition { get; }
		public float Weight { get; set; }

		public PoseInstructions(PoseTransition poseTransition, float weight)
		{
			Transition = poseTransition;
			Weight = weight;
		}
	}
}
