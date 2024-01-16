using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "PoseSequenceBlendTree", menuName = "ScriptableObjects/Animation/PoseSequenceBlendTree")]
	public class PoseSequenceBlendTree : ScriptableObject
	{
		[SerializeField] private List<PoseBlendTreeSequence> blendTree;

		public PoserStruct GetInstructions(Vector3 position, float time)
		{
			var instructions = GetPoseBlend(position);
			PoseTransition a = instructions.from.Evaluate(time);
			PoseTransition b = instructions.to.Evaluate(time);
			return new PoserStruct(a, b, instructions.interpolation);
		}

		public (IPoseSequence from, IPoseSequence to, float interpolation) GetPoseBlend(Vector2 position)
		{
			return GetPoseBlend(new Vector3(position.x, 0f, position.y));
		}

		// TODO: Create new method for proper directional blending with more than 2 sequences.
		public (IPoseSequence from, IPoseSequence to, float interpolation) GetPoseBlend(Vector3 position)
		{
			if (blendTree.Count < 2)
			{
				SpaxDebug.Error($"PoserBlendTree requires at least 2 pose sequences.");
				return default;
			}

			float closestDistance = float.MaxValue;
			PoseBlendTreeSequence closest = null;
			float secondDistance = float.MaxValue;
			PoseBlendTreeSequence second = null;
			foreach (PoseBlendTreeSequence sequence in blendTree)
			{
				float distance = Vector3.Distance(position, sequence.Position);
				if (distance < closestDistance)
				{
					secondDistance = closestDistance;
					second = closest;
					closestDistance = distance;
					closest = sequence;
				}
				else if (distance < secondDistance)
				{
					secondDistance = distance;
					second = sequence;
				}
			}

			if (closest == null || second == null)
			{
				SpaxDebug.Error("Could not complete blend", $"{name}, pos={position}, closest={closest}, second={second}");
				return (null, null, 0f);
			}

			Vector3Extensions.InverseLerp(second.Position, closest.Position, position, out float interpolation);
			return (second.Sequence, closest.Sequence, Mathf.Clamp01(interpolation));
		}
	}
}
