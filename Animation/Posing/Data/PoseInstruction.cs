namespace SpaxUtils
{
	/// <summary>
	/// Interface containing a pose transition and its effective weight.
	/// </summary>
	public struct PoseInstruction
	{
		public PoseTransition Transition { get; }
		public float Weight { get; set; }

		public PoseInstruction(PoseTransition poseTransition, float weight)
		{
			Transition = poseTransition;
			Weight = weight;
		}
	}
}
