using UnityEngine;

namespace SpaxUtils
{
	public class CameraSpeedSense : EntityComponentMono
	{
		protected Vector3 Position => useAgentAsSource ? agent.Transform.position : cameraWrapper.Position;

		[SerializeField] private bool useAgentAsSource;
		[Header("FOV.Velocity")]
		[SerializeField] private float velocitySmoothing = 0.2f;
		[SerializeField] private float upperVelocity = 10f;
		[SerializeField] private AnimationCurve velocityCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 1f)] private float velocityInfluence = 0.25f;
		[Header("FOV.Acceleration")]
		[SerializeField] private float accelerationSmoothing = 0.2f;
		[SerializeField] private float upperAcceleration = 1f;
		[SerializeField] private AnimationCurve accelerationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
		[SerializeField, Range(0f, 1f)] private float accelerationInfluence = 0.25f;

		private IAgent agent;
		private CineCameraWrapper cameraWrapper;

		private FloatOperationModifier fovMod;
		private Vector3 lastPos;
		private SmoothFloat velocity;
		private float lastVelocity;
		private SmoothFloat acceleration;

		public void InjectDependencies(IAgent agent, CineCameraWrapper cameraWrapper)
		{
			this.agent = agent;
			this.cameraWrapper = cameraWrapper;
		}

		protected void Start()
		{
			int smoothingCapacity = Mathf.RoundToInt(velocitySmoothing / Time.fixedDeltaTime);
			velocity = new SmoothFloat(smoothingCapacity);
			acceleration = new SmoothFloat(smoothingCapacity);
			fovMod = new FloatOperationModifier(ModMethod.Additive, Operation.Multiply, 1f);
			cameraWrapper.FOV.AddModifier(fovMod);
			lastPos = Position;
		}

		protected void FixedUpdate()
		{
			Vector3 delta = (Position - lastPos) / Time.fixedDeltaTime;
			velocity.Push(delta.magnitude);
			lastPos = Position;
			acceleration.Push(velocity - lastVelocity);
			lastVelocity = velocity;

			float vi = CalculateInfluence(velocity, upperVelocity, velocityCurve, velocityInfluence);
			float ai = CalculateInfluence(acceleration, upperAcceleration, accelerationCurve, accelerationInfluence);
			fovMod.Value = 1f + vi + ai;
			//SpaxDebug.Log($"fovMod={fovMod.Value}", $"v={velocity.GetValue()}, vi={vi}, a={acceleration.GetValue()}, ai={ai}");

			float CalculateInfluence(float x, float m, AnimationCurve c, float i)
			{
				float t = x / m;
				return t.Abs().Clamp01().Evaluate(c) * t.Sign() * i;
			}
		}
	}
}
