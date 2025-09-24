using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "MovementInputSettings", menuName = "ScriptableObjects/MovementInputSettings")]
	public class MovementInputSettings : ScriptableObject, IService
	{
		public float ChangeThreshold => changeThreshold;
		public AnimationCurve InputRamp => inputRamp;
		public float MinimumInput => minimumInput;
		public AnimationCurve AccelerationCurve => accelerationCurve;
		public Vector2 AccelerationTime => accelerationTime;
		public AnimationCurve DecelerationCurve => decelerationCurve;
		public Vector2 DecelerationTime => decelerationTime;

		[SerializeField] private float changeThreshold = 20f;
		[SerializeField] private AnimationCurve inputRamp = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 1f), new Keyframe(1f, 1f, -1f, 0f));
		[SerializeField] private float minimumInput = 0.1f;
		[SerializeField] private AnimationCurve accelerationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, MinMaxRange(0.01f, 2f)] private Vector2 accelerationTime = new Vector2(0.2f, 1f);
		[SerializeField] private AnimationCurve decelerationCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
		[SerializeField, MinMaxRange(0.01f, 2f)] private Vector2 decelerationTime = new Vector2(0.4f, 1f);
	}
}
