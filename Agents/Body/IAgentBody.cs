using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentBody : IEntityComponent, IHeadProvider
	{
		/// <summary>
		/// Value that scales all operations which are relative to the body.
		/// </summary>
		float Scale { get; }

		bool HasRigidbody { get; }
		RigidbodyWrapper RigidbodyWrapper { get; }
		float DefaultMass { get; }

		bool HasAnimator { get; }
		AnimatorWrapper AnimatorWrapper { get; }

		SkinnedMeshRenderer ReferenceMesh { get; }
		Transform SkeletonRootBone { get; }
		IReadOnlyList<Transform> Skeleton { get; }

		Vector3 Center { get; }
	}
}
