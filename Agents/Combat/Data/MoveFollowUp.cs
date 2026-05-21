using System;
using System.Collections.Generic;
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
		public IReadOnlyList<IConditional> Conditions => conditions?.AsReadOnly() ?? Array.Empty<IConditional>();

		[SerializeField] private PerformanceState state;
		[SerializeField] private ConditionalList conditions;
	}
}
