using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Animates the body root transform downward on landing impact using a spring simulation.
	/// Continues the fall velocity into a dip, then bounces back.
	/// FinalIK Biped Grounder keeps feet planted while the body dips, creating a
	/// natural "legs absorbing impact" effect.
	/// </summary>
	public class LandingImpactSpring : EntityComponentMono
	{
		[SerializeField] private UpdateMode updateMode;

		[Header("Spring")]
		[SerializeField, Tooltip("Maximum downward dip distance in local units.")]
		private float maxDip = 0.3f;
		[SerializeField, Tooltip("Spring stiffness. Higher = snappier return. Keep low for heavy-impact linger.")]
		private float stiffness = 40f;
		[SerializeField, Tooltip("Spring damping. Higher = less oscillation. " +
			"Balance with stiffness: low stiffness + moderate damping = lingers at max dip before returning.")]
		private float damping = 6f;

		private RigidbodyWrapper rigidbodyWrapper;
		private GrounderComponent grounder;
		private CallbackService callbackService;

		// Spring state.
		private float springPosition;
		private float springVelocity;
		private bool isActive;

		public void InjectDependencies(RigidbodyWrapper rigidbodyWrapper, GrounderComponent grounder,
			CallbackService callbackService)
		{
			this.rigidbodyWrapper = rigidbodyWrapper;
			this.grounder = grounder;
			this.callbackService = callbackService;
		}

		protected void OnEnable()
		{
			grounder.LandedEvent += OnLanded;
			callbackService.SubscribeUpdate(updateMode, this, UpdateSpring);
		}

		protected void OnDisable()
		{
			grounder.LandedEvent -= OnLanded;
			if (callbackService != null)
			{
				callbackService.UnsubscribeUpdates(this);
			}
		}

		private void OnLanded(float impactSpeed, RaycastHit hit)
		{
			// Continue the actual fall velocity into the spring.
			// The spring + damping + maxDip clamp handle deceleration naturally.
			// If already springing, add to existing velocity instead of replacing.
			springVelocity -= impactSpeed;
			isActive = true;
		}

		private void UpdateSpring(float delta)
		{
			if (!isActive)
			{
				return;
			}

			float scaledDelta = delta * EntityTimeScale;

			// Spring force toward rest position (0).
			float springForce = -stiffness * springPosition;

			// Damping force opposing velocity.
			float dampingForce = -damping * springVelocity;

			// Integrate.
			springVelocity += (springForce + dampingForce) * scaledDelta;
			springPosition += springVelocity * scaledDelta;

			// Clamp to max dip (spring can't push through the ground further than maxDip).
			if (springPosition < -maxDip)
			{
				springPosition = -maxDip;
				// Kill velocity that would push further, but allow it to bounce back.
				if (springVelocity < 0f)
				{
					springVelocity = 0f;
				}
			}

			// Don't let the body float above rest position (no upward overshoot).
			if (springPosition > 0f)
			{
				springPosition = 0f;
				if (springVelocity > 0f)
				{
					springVelocity = 0f;
				}
			}

			// Deactivate when settled.
			if (Mathf.Abs(springPosition) < 0.001f && Mathf.Abs(springVelocity) < 0.001f)
			{
				springPosition = 0f;
				springVelocity = 0f;
				isActive = false;
			}

			// Apply to local position. Only Y axis, preserving any other local offset.
			Vector3 localPos = transform.localPosition;
			localPos.y = springPosition;
			transform.localPosition = localPos;
		}
	}
}
