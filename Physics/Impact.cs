using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// Struct used for keeping track of a custom physics impact.
	/// </summary>
	[Serializable]
	public struct Impact
	{
		public Vector3 Momentum { get { return momentum; } set { momentum = value; } }
		public float InitialForce { get { return force; } set { force = value; } }
		public bool IgnoreMass { get { return ignoreMass; } set { ignoreMass = value; } }
		public bool Relative { get { return relative; } set { relative = value; } }
		public float RemainingForce { get; set; }

		/// <summary>
		/// Current progress of impact, 1 being fresh and 0 fully absorbed.
		/// </summary>
		public float Progress => RemainingForce <= 0f ? 0f : Mathf.Clamp01(RemainingForce / InitialForce);

		[SerializeField] private Vector3 momentum;
		[SerializeField] private float force;
		[SerializeField] private bool ignoreMass;
		[SerializeField] private bool relative;

		public Impact(Vector3 momentum, float force, bool ignoreMass = false, bool relative = false)
		{
			this.momentum = momentum;
			this.force = force;
			RemainingForce = force;
			this.ignoreMass = ignoreMass;
			this.relative = relative;
		}

		/// <summary>
		/// Have a rigidbody absorb a hit from this impact.
		/// </summary>
		/// <param name="rigidbody">The rigidbody which is to absorb a piece of this impact.</param>
		/// <param name="scale">The amount with which to scale the absorbed impact.</param>
		/// <returns>Whether the impact has been fully absorbed.</returns>
		public Impact Absorb(Rigidbody rigidbody, out bool fullyAbsorbed)
		{
			Vector3 force = rigidbody.velocity.CalculateForce(Relative ? rigidbody.transform.TransformDirection(Momentum) : Momentum);
			rigidbody.AddForce(force, IgnoreMass ? ForceMode.Acceleration : ForceMode.Force);
			RemainingForce -= force.magnitude;
			fullyAbsorbed = RemainingForce < Mathf.Epsilon;
			SpaxDebug.Log($"Absorb Impact: {rigidbody.gameObject.name}:", $"M={Momentum}, inF={InitialForce}, F={force}, r={RemainingForce}");
			return this;
		}

		/// <summary>
		/// Returns a copy of the impact for modification use.
		/// </summary>
		public Impact NewCopy()
		{
			return new Impact(momentum, force, ignoreMass, relative);
		}

		public override string ToString()
		{
			return $"Impact(m{Momentum} f{InitialForce} => {Progress * 100f}% f{RemainingForce})";
		}
	}
}
