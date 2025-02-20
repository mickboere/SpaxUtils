using UnityEngine;

namespace SpaxUtils
{
	public interface IMovementInputSettings
	{
		float ChangeThreshold { get; }
		AnimationCurve InputRamp { get; }
		float MinimumInput { get; }
		AnimationCurve AccelerationCurve { get; }
		Vector2 AccelerationTime { get; }
		AnimationCurve DecelerationCurve { get; }
		Vector2 DecelerationTime { get; }
	}
}
