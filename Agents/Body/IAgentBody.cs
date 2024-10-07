using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentBody : IEntityComponent, IHeadProvider
	{
		/// <summary>
		/// The unmodified default base mass of this agent body.
		/// </summary>
		float BaseMass { get; }

		/// <summary>
		/// The unmodified default base movement speed of this agent body.
		/// </summary>
		float BaseSpeed { get; }

		/// <summary>
		/// The unmodified default base size of this agent body when it is at rest.
		/// </summary>
		Vector3 BaseSize { get; }

		/// <summary>
		/// Active scale of the agent body.
		/// </summary>
		float Scale { get; }

		RigidbodyWrapper RigidbodyWrapper { get; }
		CapsuleCollider Bumper { get; }
		AnimatorWrapper AnimatorWrapper { get; }
		Transform SkeletonRootBone { get; }
		IReadOnlyList<Transform> Skeleton { get; }
		IReadOnlyList<Renderer> Renderers { get; }

		bool HasRigidbody { get; }
		bool HasAnimator { get; }

		Vector3 Center { get; }
	}
}
