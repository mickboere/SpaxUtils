using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class PoseBlendMapEntry
	{
		/// <summary>
		/// The pose sequence at this blendtree point.
		/// </summary>
		public PoseSequence Sequence => sequence;

		/// <summary>
		/// The location of this sequence within the blend tree.
		/// </summary>
		public Vector3 Position => position;

		[SerializeField] private PoseSequence sequence;
		[SerializeField] private Vector3 position;

		/// <summary>
		/// Runtime constructor for creating override entries.
		/// </summary>
		public PoseBlendMapEntry(PoseSequence sequence, Vector3 position)
		{
			this.sequence = sequence;
			this.position = position;
		}
	}
}
