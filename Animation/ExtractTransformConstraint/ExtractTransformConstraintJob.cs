using UnityEngine;
using UnityEngine.Animations.Rigging;

public struct ExtractTransformConstraintJob : IWeightedAnimationJob
{
	public ReadWriteTransformHandle bone;

	public FloatProperty jobWeight { get; set; }

	public Vector3Property position;
	public Vector4Property rotation;

	public void ProcessRootMotion(UnityEngine.Animations.AnimationStream stream)
	{ }

	public void ProcessAnimation(UnityEngine.Animations.AnimationStream stream)
	{
		AnimationRuntimeUtils.PassThrough(stream, this.bone);

		Vector3 pos = this.bone.GetPosition(stream);
		Quaternion rot = this.bone.GetRotation(stream);

		this.position.Set(stream, pos);
		this.rotation.Set(stream, new Vector4(rot.x, rot.y, rot.z, rot.w));
	}
}
