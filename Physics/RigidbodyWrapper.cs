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
		public Rigidbody Rigidbody => rigidbody;

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

		/// <summary>
		/// Current velocity of the <see cref="UnityEngine.Rigidbody"/>.
		/// </summary>
		public Vector3 Velocity { get => Rigidbody.velocity; set => Rigidbody.velocity = value; }

		/// <summary>
		/// The current change in velocity.
		/// </summary>
		public Vector3 Acceleration { get; private set; }

		/// <summary>
		/// Current relative velocity of the <see cref="UnityEngine.Rigidbody"/>.
		/// </summary>
		public Vector3 RelativeVelocity => transform.InverseTransformDirection(Velocity);

		/// <summary>
		/// The current relative change in velocity.
		/// </summary>
		public Vector3 RelativeAcceleration => transform.InverseTransformDirection(Acceleration);

		/// <summary>
		/// Current velocity.magnitude.
		/// </summary>
		public float Speed => Velocity.magnitude;

		/// <summary>
		/// Current velocity.magnitude, not factoring in local Y velocity.
		/// </summary>
		public float HorizontalSpeed => RelativeVelocity.FlattenY().magnitude;

		public Vector3 TargetVelocity { get; set; }
		public CompositeFloat Control { get; set; } = new CompositeFloat(1f);
		public Vector3 ControlAxis { get; set; } = Vector3.one;

		/// <summary>
		/// Returns true if <see cref="Control"/> is greater than 0.5.
		/// </summary>
		public bool HasControl => Control > 0.5f;

		public float Grip { get; private set; }

		public IReadOnlyList<Impact> Impacts => impacts;
		public int ImpactCount => impacts.Count;
		public float ImpactsEffect => ImpactCount == 0 ? 0f : impacts.Max(i => i.Effect);

		public float Scale { get => scale; set { scale = value; } }
		public float Mass { get => mass; set { mass = value; } }
		public bool Automata { get => automata; set { automata = value; } }
		public float AutoControlForce { get => autoControlForce; set { autoControlForce = value; } }
		public float AutoBrakeForce { get => autoBrakeForce; set { autoBrakeForce = value; } }
		public float AutoPower { get => autoPower; set { autoPower = value; } }

		[SerializeField] new private Rigidbody rigidbody;
		[SerializeField] private float scale = 1f;
		[SerializeField] private float mass = 1f;
		[SerializeField] private int accelerationSmoothing = 3;
		[SerializeField] private bool automata = false;
		[SerializeField, Conditional(nameof(automata), hide: true)] private float autoControlForce = 2000f;
		[SerializeField, Conditional(nameof(automata), hide: true)] private float autoBrakeForce = 200f;
		[SerializeField, Conditional(nameof(automata), hide: true)] private float autoPower = 20f;
		[SerializeField, Conditional(nameof(automata), hide: true)] private Vector3 autoControlAxis = Vector3.one;

		private Vector3 lastVelocity;
		private SmoothVector3 velocityDelta;
		private List<Impact> impacts = new List<Impact>();

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

			ApplyImpacts();
			Grip = CalculateGrip();

			if (Automata)
			{
				ApplyAutomataMovement();
			}
		}

		protected void Initialize()
		{
			if (EnsureRigidbody())
			{
				rigidbody.mass = Mass;
			}

			if (Application.isPlaying)
			{
				velocityDelta = new SmoothVector3(accelerationSmoothing);
			}
		}

		#region Impacts

		public void AddImpact(Impact impact)
		{
			impacts.Add(impact.NewCopy());
		}

		public void ClearImpacts()
		{
			impacts.Clear();
		}

		private void ApplyImpacts()
		{
			for (int i = 0; i < impacts.Count; i++)
			{
				impacts[i] = impacts[i].Absorb(rigidbody, Scale, out bool absorbed);
				if (absorbed)
				{
					impacts.RemoveAt(i);
					i--;
				}
			}
		}

		#endregion

		public void ApplyAutomataMovement()
		{
			Vector3 previousControl = ControlAxis;
			ControlAxis = autoControlAxis;
			ApplyMovement(AutoControlForce, AutoBrakeForce, AutoPower);
			ControlAxis = previousControl;
		}

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
			float maxForce = Mathf.Lerp(brakeForce, controlForce, control * (1f - ImpactsEffect));
			Vector3 force = Velocity.CalculateForce(TargetVelocity * control * scale, power * scale, maxForce * scale).Localize(transform).Multiply(ControlAxis).Globalize(transform);
			Rigidbody.AddForce(force);
		}

		/// <summary>
		/// Calculate the current grip using the target velocity and current velocity.
		/// </summary>
		/// <param name="controlled">TRUE will multiply grip by control.</param>
		/// <returns>The current grip calculated using the target velocity and current velocity.</returns>
		public float CalculateGrip(bool controlled = true)
		{
			float grip = TargetVelocity == Vector3.zero || Speed.Approx(0f) ? 1f :
				Mathf.Clamp01(Rigidbody.velocity.normalized.NormalizedDot(TargetVelocity.normalized) * (Speed / TargetVelocity.magnitude));

			if (controlled)
			{
				grip *= Control;
			}

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
