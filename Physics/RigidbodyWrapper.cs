using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that wraps around Unity's <see cref="UnityEngine.Rigidbody"/> and provides additional functionality and optimizations.
	/// Implements <see cref="IDependency"/> making it easy to retrieve the <see cref="UnityEngine.Rigidbody"/>.
	/// </summary>
	[DefaultExecutionOrder(1000)]
	public class RigidbodyWrapper : MonoBehaviour, IDependency
	{
		/// <summary>
		/// The current physics iteration count for this RigidBody.
		/// </summary>
		public int PhysicsStep { get; private set; }

		/// <summary>
		/// Protected property because all modifications to the rigidbody should be done through the wrapper.
		/// </summary>
		protected Rigidbody Rigidbody => rigidbody;

		#region Transform
		public Vector3 Position
		{
			get => Rigidbody.position;
			set
			{
				Vector3 p = Rigidbody.position;
				Rigidbody.position = value;
				if (debug && log)
				{
					Log("SET Position", $"p={Position}, pP={p}");
				}
			}
		}
		public Quaternion Rotation { get => Rigidbody.rotation; set => Rigidbody.rotation = value; }
		public Vector3 Right => Rotation * Vector3.right;
		public Vector3 Left => Rotation * Vector3.left;
		public Vector3 Up => Rotation * Vector3.up;
		public Vector3 Down => Rotation * Vector3.down;
		public Vector3 Forward => Rotation * Vector3.forward;
		public Vector3 Back => Rotation * Vector3.back;
		#endregion Transform

		#region Property Wrappers

		/// <summary>
		/// Mass of the rigidbody in Kilograms.
		/// </summary>
		public float Mass { get => Rigidbody.mass; set { Rigidbody.mass = value; } }

		/// <summary>
		/// Center of mass of the rigidbody in world space
		/// </summary>
		public Vector3 CenterOfMass { get => Rigidbody.worldCenterOfMass; set => Rigidbody.centerOfMass = value.LocalizeDirection(Rigidbody.transform); }

		/// <summary>
		/// Center of mass of the rigidbody in local space.
		/// </summary>
		public Vector3 CenterOfMassRelative { get => Rigidbody.centerOfMass; set => Rigidbody.centerOfMass = value; }

		/// <summary>
		/// Velocity of the rigidbody in world space.
		/// </summary>
		public Vector3 Velocity
		{
			get => Rigidbody.linearVelocity;
			set
			{
				Vector3 p = Velocity;
				Rigidbody.linearVelocity = value;
				if (debug && log)
				{
					Log("SET Velocity", $"v={Velocity}, pV={p}");
				}
			}
		}

		/// <summary>
		/// Velocity of the rigidbody in local space.
		/// </summary>
		public Vector3 RelativeVelocity { get => Velocity.LocalizeDirection(Rigidbody.transform); set => Velocity = value.GlobalizeDirection(Rigidbody.transform); }

		/// <summary>
		/// Magnitude of the rigidbody's velocity.
		/// </summary>
		public float Speed => Velocity.magnitude;

		/// <summary>
		/// Angular velocity of the rigidbody in world space.
		/// </summary>
		public Vector3 AngularVelocity { get => Rigidbody.angularVelocity; set => Rigidbody.angularVelocity = value; }

		/// <summary>
		/// Whether the rigidbody should be exempt from the effects of the physics simulation.
		/// </summary>
		public bool IsKinematic { get => Rigidbody.isKinematic; set => Rigidbody.isKinematic = value; }

		#endregion // Property Wrappers.

		#region Custom Properties

		/// <summary>
		/// Average change in velocity of the rigidbody in world space (readonly).
		/// </summary>
		public Vector3 Acceleration { get; private set; }

		/// <summary>
		/// Average change in velocity of the rigidbody in local space (readonly).
		/// </summary>
		public Vector3 RelativeAcceleration => Acceleration.LocalizeDirection(Rigidbody.transform);

		/// <summary>
		/// The kinetic energy of this rigidbody.
		/// </summary>
		public Vector3 KineticEnergy => Velocity.KineticEnergy(Mass);

		/// <summary>
		/// Target velocity of the rigidbody in world space, used for movement mechanics.
		/// </summary>
		public Vector3 TargetVelocity { get; set; }

		/// <summary>
		/// Modifiable float value indicating the total amount of movement control.
		/// Modding it to '0' will remove all movement control.
		/// </summary>
		public CompositeFloat Control { get; } = new CompositeFloat(1f);

		/// <summary>
		/// Indicates which relative axis the rigidbody has movement control over.
		/// An axis value of '1' is 100% control, '0' is 0% control, '-1' is inverted control.
		/// </summary>
		public Vector3 ControlAxis { get; set; } = Vector3.one;

		/// <summary>
		/// Difference between velocity and target velocity.
		/// </summary>
		public float Grip { get; private set; }

		#endregion Custom Properties

		[SerializeField] new private Rigidbody rigidbody;
		[SerializeField] private int accelerationSmoothing = 3;
		[SerializeField] private float gripScale = 10f;
		[SerializeField] private bool debug;
		[SerializeField, Conditional(nameof(debug))] private bool log;
		[SerializeField, Conditional(nameof(log))] private bool forces;

		private EntityStat timeScale;
		private StatSubscription massStatSub;

		private Vector3 previousVelocity;
		private SmoothVector3 velocityDelta;
		private Vector3 previousPosition;

		public void InjectDependencies([Optional] IEntity entity)
		{
			if (entity != null)
			{
				timeScale = entity.Stats.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
				if (entity.Stats.TryGetStat(AgentStatIdentifiers.MASS, out EntityStat massStat))
				{
					massStatSub = new StatSubscription(massStat, (entityMassStat) => Mass = entityMassStat);
				}
			}
		}

		protected void OnDestroy()
		{
			massStatSub?.Dispose();
		}

		protected void Reset()
		{
			Initialize();
		}

		protected void OnValidate()
		{
			Initialize();
		}

		protected void Awake()
		{
			Initialize();
		}

		protected void FixedUpdate()
		{
			// Calculate acceleration and grip.
			Vector3 delta = Velocity - previousVelocity;
			velocityDelta.Push(delta);
			Acceleration = velocityDelta;
			Grip = CalculateGrip();

			// Manual velocity delta check.
			delta = Position - previousPosition;

			// Apply local entity timescale to physics.
			if (timeScale != null)
			{
				Position = (Position - Velocity * Time.fixedDeltaTime) + Velocity * timeScale * Time.fixedDeltaTime;
			}

			if (debug && log)
			{
				Log($"FixedUpdate:", $"V={Velocity}, pV={previousVelocity}\n" +
					$"d={delta}, dV={delta / Time.fixedDeltaTime}, P={Position}, pP={previousPosition}\n" +
					$"A={Acceleration}, G={Grip}");
			}

			previousVelocity = Velocity;
			previousPosition = Position;
			PhysicsStep++;
		}

		protected void Initialize()
		{
			EnsureRigidbody();

			if (Application.isPlaying)
			{
				velocityDelta = new SmoothVector3(accelerationSmoothing);
			}
		}

		#region Forces

		public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
		{
			float multiplier = forceMode == ForceMode.Force || forceMode == ForceMode.Acceleration ? timeScale : 1f;
			Rigidbody.AddForce(force * multiplier, forceMode);

			if (debug && log && forces)
			{
				Log($"AddForce({force} * {multiplier}, {forceMode})", $"current={Velocity}, previous={previousVelocity}");
			}
		}

		public void AddForceRelative(Vector3 force, ForceMode forceMode = ForceMode.Force)
		{
			AddForce(force.GlobalizeDirection(Rigidbody.transform), forceMode);
		}

		/// <summary>
		/// Adds an push force to the rigidbody.
		/// As opposed to <see cref="AddForce(Vector3, ForceMode)"/>, a push will only have
		/// effect on the rigidbody if it is greater than or in opposition to the body's velocity.
		/// </summary>
		/// <param name="velocity">Velocity of the incoming impact.</param>
		/// <param name="mass">Mass of the incoming impact. Leave negative to match rigidbody mass.</param>
		public void Push(Vector3 velocity, float mass = -1f)
		{
			if (velocity == Vector3.zero)
			{
				return;
			}

			if (mass <= 0)
			{
				mass = Mass;
			}

			Vector3 push = Velocity.CalculatePush(velocity, out _);
			AddForce(push * mass / Mass, ForceMode.VelocityChange);
		}

		/// <summary>
		/// Adds an impact force relative to the rigidbody.
		/// As opposed to <see cref="AddForceRelative(Vector3, ForceMode)"/>, an impact will only have
		/// effect on the rigidbody if it is greater than or in opposition to the body's velocity.
		/// </summary>
		/// <param name="velocity">Localized velocity of the incoming impact.</param>
		/// <param name="mass">Mass of the incoming impact. Leave negative to match rigidbody mass.</param>
		public void PushRelative(Vector3 velocity, float mass = -1f)
		{
			Push(velocity.GlobalizeDirection(Rigidbody.transform), mass);
		}

		/// <summary>
		/// Resets the rigidbody's velocities to zero.
		/// </summary>
		public void ResetVelocity()
		{
			Velocity = Vector3.zero;
			AngularVelocity = Vector3.zero;
		}

		#endregion

		#region Movement

		/// <summary>
		/// Applies a force that intends to reach the target velocity.
		/// </summary>
		/// <param name="targetVelocity">The desired velocity of the rigidbody. If NULL, will use <see cref="TargetVelocity"/></param>
		/// <param name="maxAcceleration">The max amount of force that can be applied per application.</param>
		/// <param name="power">Responsiveness of the applied forces.</param>
		/// <param name="ignoreControl">TRUE will always use 100% control, false will use current <see cref="Control"/> percentage.</param>
		/// <param name="scale">Float that scales every single calculation involved, used to simulate reduced mobility.</param>
		public void ApplyMovement(Vector3? targetVelocity = null, float maxAcceleration = 2000f, float maxDeceleration = 2000f, float power = 20f,
			bool ignoreControl = false, float scale = 1f)
		{
			Vector3 target = targetVelocity == null ? TargetVelocity : targetVelocity.Value;
			float control = ignoreControl ? 1f : Control;
			float acceleration = target == Vector3.zero ? 0f : Velocity == Vector3.zero ? 1f :
				Velocity.normalized.NormalizedDot((target - Velocity).normalized);
			float maxForce = maxDeceleration.Lerp(maxAcceleration, acceleration);
			Vector3 force = Velocity.CalculateForce(
				target * control * scale,
				power * scale * (timeScale ?? 1f),
				maxForce * scale * (timeScale ?? 1f))
				.LocalizeDirection(transform).Multiply(ControlAxis).GlobalizeDirection(transform);

			AddForce(force);
		}

		/// <summary>
		/// Calculate the current grip using the target velocity and current velocity.
		/// </summary>
		/// <returns>The current grip calculated using the target velocity and current velocity.</returns>
		public float CalculateGrip()
		{
			float grip = Mathf.Clamp01((Velocity.Multiply(ControlAxis) - TargetVelocity * Control).magnitude * (1f / gripScale)).ReverseInOutCubic();
			return grip;
		}

		#endregion Movement

		private bool EnsureRigidbody()
		{
			if (rigidbody == null)
			{
				rigidbody = GetComponentInParent<Rigidbody>();
			}
			return rigidbody != null;
		}

		private void Log(string a, string b)
		{
			SpaxDebug.Log($"<b>p[{PhysicsStep}]</b> {a}", b);
		}
	}
}
