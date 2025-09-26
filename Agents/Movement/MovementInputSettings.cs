using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "MovementInputSettings", menuName = "ScriptableObjects/MovementInputSettings")]
	public class MovementInputSettings : ScriptableObject, IService
	{
		public float MinimumInput => minimumInput;
		public AnimationCurve InputRamp => inputRamp;
		public float Smoothing => smoothing;
		public float MaxSmoothingVelocity => maxSmoothingVelocity;

		[SerializeField, Range(0f, 1f)] private float minimumInput = 0.1f;
		[SerializeField] private AnimationCurve inputRamp = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 1f), new Keyframe(1f, 1f, -1f, 0f));
		[SerializeField, Range(0f, 1f)] private float smoothing = 0.1f;
		[SerializeField] private float maxSmoothingVelocity = 10f;
	}
}
