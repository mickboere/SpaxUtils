using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// MonoBehaviour that wraps around Unity's <see cref="UnityEngine.Rigidbody"/> and provides additional functionality and optimizations.
	/// Implements <see cref="IDependency"/> making it easy to retrieve the <see cref="UnityEngine.Rigidbody"/>.
	/// </summary>
	[DefaultExecutionOrder(-100)]
	public class RigidbodyWrapper : MonoBehaviour, IDependency
	{
		/// <summary>
		/// Protected property because all modifications to the rigidbody should be done through the wrapper.
		/// </summary>
		protected Rigidbody Rigidbody => rigidbody;

		#region Orientation
		public Vector3 Position { get => Rigidbody.position; set => Rigidbody.position = value; }
		public Quaternion Rotation { get => Rigidbody.rotation; set => Rigidbody.rotation = value; }
		public Vector3 Right => Rotation * Vector3.right;
		public Vector3 Left => Rotation * Vector3.left;
		public Vector3 Up => Rotation * Vector3.up;
		public Vector3 Down => Rotation * Vector3.down;
		public Vector3 Forward => Rotation * Vector3.forward;
		public Vector3 Back => Rotation * Vector3.back;
		#endregion

		#region Property Wrappers

		/// <summary>
		/// Mass of the rigidbody in Kilograms.
		/// </summary>
		public float Mass { get => Rigidbody.mass; set { Rigidbody.mass = value; } }

		/// <summary>
		/// Center of mass of the rigidbody in world space
		/// </summary>
		public Vector3 CenterOfMass { get => Rigidbody.worldCenterOfMass; set => Rigidbody.centerOfMass = value.Localize(Rigidbody.transform); }

		/// <summary>
		/// Center of mass of the rigidbody in local space.
		/// </summary>
		public Vector3 CenterOfMassRelative { get => Rigidbody.centerOfMass; set => Rigidbody.centerOfMass = value; }

		/// <summary>
		/// Velocity of the rigidbody in world space.
		/// </summary>
		public Vector3 Velocity { get => Rigidbody.velocity; set => Rigidbody.velocity = value; }

		/// <summary>
		/// Velocity of the rigidbody in local space.
		/// </summary>
		public Vector3 RelativeVelocity { get => Velocity.Localize(Rigidbody.transform); set => Velocity = value.Globalize(Rigidbody.transform); }

		/// <summary>
		/// Magnitude of the rigidbody's velocity.
		/// </summary>
		public float Speed => Velocity.magnitude;

		/// <summary>
		/// Angular velocity of the rigidbody in world space.
		/// </summary>
		public Vector3 AngularVelocity { get => Rigidbody.angularVelocity; set => Rigidbody.angularVelocity = value; }

		#endregion // Property Wrappers.

		/// <summary>
		/// Average change in velocity of the rigidbody in world space (readonly).
		/// </summary>
		public Vector3 Acceleration { get; private set; }

		/// <summary>
		/// Average change in velocity of the rigidbody in local space (readonly).
		/// </summary>
		public Vector3 RelativeAcceleration => Acceleration.Localize(Rigidbody.transform);

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
		public CompositeFloat Control { get; set; } = new CompositeFloat(1f);

		/// <summary>
		/// Indicates which relative axis the rigidbody has movement control over.
		/// An axis value of '1' is 100% control, '0' is 0% control, '-1' is inverted control.
		/// </summary>
		public Vector3 ControlAxis { get; set; } = Vector3.one;

		/// <summary>
		/// Difference between velocity and target velocity.
		/// </summary>
		public float Grip { get; private set; }

		[SerializeField] new private Rigidbody rigidbody;
		[SerializeField] private int accelerationSmoothing = 3;

		private EntityStat timeScale;
		private StatSubscription massStatSub;

		private Vector3 lastVelocity;
		private SmoothVector3 velocityDelta;

		public void InjectDependencies([Optional] IEntity entity)
		{
			if (entity != null)
			{
				timeScale = entity.GetStat(EntityStatIdentifiers.TIMESCALE, true, 1f);
				if (entity.TryGetStat(EntityStatIdentifiers.MASS, out EntityStat massStat))
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
			Vector3 delta = Velocity - lastVelocity;
			velocityDelta.Push(delta);
			Acceleration = velocityDelta;
			lastVelocity = Velocity;

			//ApplyImpacts();
			Grip = CalculateGrip();

			// Apply local entity timescale.
			if (timeScale != null)
			{
				Position = (Position - Velocity * Time.fixedDeltaTime) + Velocity * timeScale * Time.fixedDeltaTime;
			}
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

		public void AddForce(Vector3 force, ForceMode forceMode)
		{
			Rigidbody.AddForce(force, forceMode);
		}

		public void AddForceRelative(Vector3 force, ForceMode forceMode)
		{
			Rigidbody.AddForce(force.Globalize(Rigidbody.transform), forceMode);
		}

		/// <summary>
		/// Adds an impact force to the rigidbody.
		/// As opposed to <see cref="AddForce(Vector3, ForceMode)"/>, an impact will only have
		/// effect on the rigidbody if it is greater than or in opposition to the body's velocity.
		/// </summary>
		/// <param name="velocity">Velocity of the incoming impact.</param>
		/// <param name="mass">Mass of the incoming impact. Leave negative to match rigidbody mass.</param>
		public void AddImpact(Vector3 velocity, float mass = -1f)
		{
			if (velocity == Vector3.zero)
			{
				return;
			}

			if (mass < 0)
			{
				mass = Mass;
			}

			Vector3 kE = velocity.KineticEnergy(mass);
			Vector3 impactForce = (kE - KineticEnergy) / velocity.magnitude * Time.fixedDeltaTime;

			Velocity += impactForce;
		}

		/// <summary>
		/// Adds an impact force relative to the rigidbody.
		/// As opposed to <see cref="AddForceRelative(Vector3, ForceMode)"/>, an impact will only have
		/// effect on the rigidbody if it is greater than or in opposition to the body's velocity.
		/// </summary>
		/// <param name="velocity">Localized velocity of the incoming impact.</param>
		/// <param name="mass">Mass of the incoming impact. Leave negative to match rigidbody mass.</param>
		public void AddImpactRelative(Vector3 velocity, float mass = -1f)
		{
			AddImpact(velocity.Globalize(Rigidbody.transform), mass);
		}

		#endregion

		/// <summary>
		/// Apply a force to reach the <see cref="TargetVelocity"/>
		/// </summary>
		/// <param name="controlForce">Force used at 100% control.</param>
		/// <param name="brakeForce">Force used at 0% control.</param>
		/// <param name="power">Scaling of forces; responsiveness.</param>
		/// <param name="ignoreControl">TRUE will always use 100% control, false will use current <see cref="Control"/> percentage.</param>
		public void ApplyMovement(float controlForce = 2000f, float brakeForce = 200f, float power = 20f, bool ignoreControl = false, float scale = 1f)
		{
			float control = ignoreControl ? 1f : Control;
			float maxForce = Mathf.Lerp(brakeForce, controlForce, control);
			Vector3 force = Velocity.CalculateForce(
				TargetVelocity * control * scale,
				power * scale * (timeScale ?? 1f),
				maxForce * scale * (timeScale ?? 1f))
				.Localize(transform).Multiply(ControlAxis).Globalize(transform);
			Rigidbody.AddForce(force);
		}

		/// <summary>
		/// Calculate the current grip using the target velocity and current velocity.
		/// </summary>
		/// <returns>The current grip calculated using the target velocity and current velocity.</returns>
		public float CalculateGrip()
		{
			float grip = TargetVelocity == Vector3.zero || Speed.Approx(0f) ? 1f :
				Mathf.Clamp01(Rigidbody.velocity.normalized.NormalizedDot(TargetVelocity.normalized) * (Speed / TargetVelocity.magnitude));

			if (grip > 1f)
			{
				grip = 1f / grip;
			}

			return grip;
		}

		private bool EnsureRigidbody()
		{
			if (rigidbody == null)
			{
				rigidbody = GetComponentInParent<Rigidbody>();
			}
			return rigidbody != null;
		}
	}
}
