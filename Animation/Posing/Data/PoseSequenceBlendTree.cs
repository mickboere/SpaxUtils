using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaxUtils
{
	[CreateAssetMenu(fileName = "PoseSequenceBlendTree", menuName = "ScriptableObjects/Animation/PoseSequenceBlendTree")]
	public class PoseSequenceBlendTree : ScriptableObject
	{
		[SerializeField] private List<PoseBlendTreeSequence> blendTree;

		public (IPoseSequence from, IPoseSequence to, float interpolation) GetInstructions(Vector2 position)
		{
			return GetInstructions(new Vector3(position.x, 0f, position.y));
		}

		public (IPoseSequence from, IPoseSequence to, float interpolation) GetInstructions(Vector3 position)
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

			Vector3Extensions.InverseLerp(second.Position, closest.Position, position, out float interpolation);
			return (second.Sequence, closest.Sequence, Mathf.Clamp01(interpolation));
		}
	}
}
