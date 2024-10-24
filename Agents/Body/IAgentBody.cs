﻿using System.Collections.Generic;
using UnityEngine;

namespace SpaxUtils
{
	public interface IAgentBody : IEntityComponent, IHeadProvider
	{
		/// <summary>
		/// Value that (should) scale all operations which are relative to the body.
		/// </summary>
		float Scale { get; }

		bool HasRigidbody { get; }
		RigidbodyWrapper RigidbodyWrapper { get; }
		CapsuleCollider Bumper { get; }
		float BaseMass { get; }
		float BaseSpeed { get; }

		bool HasAnimator { get; }
		AnimatorWrapper AnimatorWrapper { get; }

		Transform SkeletonRootBone { get; }
		IReadOnlyList<Transform> Skeleton { get; }
		IReadOnlyList<Renderer> Renderers { get; }

		Vector3 Center { get; }
	}
}
