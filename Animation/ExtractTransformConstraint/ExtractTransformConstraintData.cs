using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[Serializable]
public struct ExtractTransformConstraintData : IAnimationJobData
{
	[SyncSceneToStream] public Transform bone;

	public Vector3 position;
	public Quaternion rotation;

	public bool IsValid()
	{
		return bone != null;
	}

	public void SetDefaultValues()
	{
		this.bone = null;

		this.position = Vector3.zero;
		this.rotation = Quaternion.identity;
	}
}
