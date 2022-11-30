using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Struct used for keeping track of a physics impact.
	/// </summary>
	[Serializable]
	public struct Impact
	{
		public Vector3 Momentum { get { return momentum; } set { momentum = value; } }
		public float InitialForce { get { return force; } set { force = value; } }
		public bool IgnoreMass { get { return ignoreMass; } set { ignoreMass = value; } }
		public bool Relative { get { return relative; } set { relative = value; } }
		public float CurrentForce { get; set; }

		/// <summary>
		/// Current progress of impact, 1 being fresh and 0 fully absorbed.
		/// </summary>
		public float Progress => CurrentForce <= 0f ? 0f : Mathf.Clamp01(CurrentForce / InitialForce);

		/// <summary>
		/// Current effect of this impact according to its progress.
		/// </summary>
		public float Effect => CalculateEffect(Progress);

		[SerializeField] private Vector3 momentum;
		[SerializeField] private float force;
		[SerializeField] private bool ignoreMass;
		[SerializeField] private bool relative;
		[SerializeField] private float delay;
		[SerializeField] private bool realtime;

		private Timer delayTimer;

		public Impact(Vector3 momentum, float force, bool ignoreMass = false, bool relative = false, float delay = 0f, bool realtime = false)
		{
			this.momentum = momentum;
			this.force = force;
			CurrentForce = force;
			this.ignoreMass = ignoreMass;
			this.relative = relative;

			this.delay = delay;
			this.realtime = realtime;
			delayTimer = new Timer(delay, realtime);
		}

		/// <summary>
		/// Have a rigidbody absorb a hit from this impact.
		/// </summary>
		/// <param name="rigidbody">The rigidbody which is to absorb a piece of this impact.</param>
		/// <param name="scale">The amount with which to scale the absorbed impact.</param>
		/// <returns>Whether the impact has been fully absorbed.</returns>
		public Impact Absorb(Rigidbody rigidbody, float scale, out bool fullyAbsorbed)
		{
			if (delayTimer)
			{
				// Delay hasn't expired yet.
				fullyAbsorbed = false;
				return this;
			}

			Vector3 force = rigidbody.velocity.CalculateForce(Relative ? rigidbody.transform.TransformDirection(Momentum) : Momentum, 1f);
			rigidbody.AddForce(force * Effect * scale, IgnoreMass ? ForceMode.Acceleration : ForceMode.Force);
			CurrentForce -= force.magnitude * Time.fixedDeltaTime;
			fullyAbsorbed = CurrentForce <= Mathf.Epsilon;
			return this;
		}

		/// <summary>
		/// Returns a copy of the impact for modification use.
		/// </summary>
		public Impact NewCopy()
		{
			return new Impact(momentum, force, ignoreMass, relative, delay, realtime);
		}

		private float CalculateEffect(float x)
		{
			return x.InOutCubic();
		}

		public override string ToString()
		{
			return $"Impact(m{Momentum} f{InitialForce} => {Progress * 100f}% f{CurrentForce})";
		}
	}
}
