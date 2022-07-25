using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentBody : IEntityComponent
	{
		/// <summary>
		/// Value that scales all operations which are relative to the body.
		/// </summary>
		float Scale { get; }

		bool HasRigidbody { get; }
		bool HasAnimator { get; }
		RigidbodyWrapper RigidbodyWrapper { get; }
		AnimatorWrapper AnimatorWrapper { get; }
		SkinnedMeshRenderer ReferenceMesh { get; }
		Transform SkeletonRootBone { get; }
		IReadOnlyList<Transform> Skeleton { get; }
		Vector3 Center { get; }
	}
}
