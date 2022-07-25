using System;
using UnityEngine;

namespace SpaxUtils
{
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

		public Impact(Vector3 momentum, float force, bool ignoreMass = false, bool relative = false)
		{
			this.momentum = momentum;
			this.force = force;
			CurrentForce = force;
			this.ignoreMass = ignoreMass;
			this.relative = relative;
		}

		/// <summary>
		/// Have a rigidbody absorb a hit from this impact.
		/// </summary>
		/// <param name="rigidbody">The rigidbody which is to absorb a piece of this impact.</param>
		/// <param name="scale">The amount with which to scale the absorbed impact.</param>
		/// <returns>Whether the impact has been fully absorbed.</returns>
		public Impact Absorb(Rigidbody rigidbody, float scale, out bool fullyAbsorbed)
		{
			Vector3 force = rigidbody.velocity.CalculateForce(Relative ? rigidbody.transform.TransformDirection(Momentum) : Momentum, 1f);
			rigidbody.AddForce(force * Effect * scale, IgnoreMass ? ForceMode.Acceleration : ForceMode.Force);
			CurrentForce -= force.magnitude * Time.fixedDeltaTime;
			fullyAbsorbed = CurrentForce <= Mathf.Epsilon;
			return this;
		}

		public Impact New()
		{
			return new Impact(momentum, force, ignoreMass, relative);
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
