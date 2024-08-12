using System;
using UnityEngine;

namespace SpaxUtils
{
	/// <summary>
	/// An <see cref="ActMovePair"/> that also defines a <see cref="PerformanceState"/> in which the the follow-up move can be performed.
	/// </summary>
	[Serializable]
	public class MoveFollowUp : ActMovePair
	{
		public PerformanceState State => state;

		[SerializeField] private PerformanceState state;
	}
}
