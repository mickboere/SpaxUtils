namespace SpaxUtils
{
	/// <summary>A swap whose button is still down, counting toward the unarm threshold.</summary>
	public class PendingSwap
	{
		public ArmState Arm;
		public float Time;
	}
}
