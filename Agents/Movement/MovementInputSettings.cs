using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "MovementInputSettings", menuName = "ScriptableObjects/MovementInputSettings")]
	public class MovementInputSettings : ScriptableObject
	{
		public float ChangeThreshold => changeThreshold;
		public AnimationCurve InputRamp => inputRamp;
		public float MinimumInput => minimumInput;
		public float AccelerationTime => accelerationTime;
		public float DecelerationTime => decelerationTime;

		[SerializeField] private float changeThreshold = 20f;
		[SerializeField] private AnimationCurve inputRamp = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 1f), new Keyframe(1f, 1f, -1f, 0f));
		[SerializeField] private float minimumInput = 0.1f;
		[SerializeField] private AnimationCurve accelerationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField] private float accelerationTime = 0.6f;
		[SerializeField] private AnimationCurve decelerationCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
		[SerializeField] private float decelerationTime = 0.4f;

		public float EvaluateInputRamp(float input)
		{
			return inputRamp.Evaluate(input);
		}

		public float EvaluateAcceleration(float time)
		{
			return accelerationCurve.Evaluate(time / accelerationTime);
		}

		public float EvaluateAccelerationNormalized(float value)
		{
			return accelerationCurve.Evaluate(value);
		}

		public float EvaluateDeceleration(float time)
		{
			return decelerationCurve.Evaluate(time / accelerationTime);
		}

		public float EvaluateDecelerationNormalized(float value)
		{
			return decelerationCurve.Evaluate(value);
		}
	}
}
