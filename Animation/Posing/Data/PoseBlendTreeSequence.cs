using System;
using UnityEngine;

namespace SpaxUtils
{
	[Serializable]
	public class PoseBlendTreeSequence
	{
		/// <summary>
		/// The location of this sequence within the blend tree.
		/// </summary>
		public Vector3 Position => position;

		public PoseSequence Sequence => sequence;

		[SerializeField] private Vector3 position;
		[SerializeField] private PoseSequence sequence;
	}
}
